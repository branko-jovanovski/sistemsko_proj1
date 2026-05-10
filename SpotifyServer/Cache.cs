using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SpotifyApiServer
{
    public class CacheItem
    {
        public string podaci { get; set; }
        public DateTime vremeIsticanja { get; set; }
        public bool preuzimanjeUToku { get; set; } = false;
    }

    public class Cache
    {
        private readonly Dictionary<string, CacheItem> storage = new Dictionary<string, CacheItem>();
        private readonly object zakljucavanje = new object();
        private readonly TimeSpan trajanjeKesa;

        public Cache(TimeSpan ttl)
        {
            trajanjeKesa = ttl;

            
            Thread cistac = new Thread(AktivnoCiscenjeKesa)
            {
                IsBackground = true,
                Name = "Nit-CistacKesa"
            };
            cistac.Start();
        }

        private void AktivnoCiscenjeKesa()
        {
            while (true)
            {
                Thread.Sleep(120000); 

                int brojObrisanih = 0;

                lock (zakljucavanje)
                {
                    DateTime sada = DateTime.Now;

                    List<string> kljuceviZaBrisanje = new List<string>();

                    foreach (var par in storage)
                    {
                        if (sada >= par.Value.vremeIsticanja && !par.Value.preuzimanjeUToku)
                        {
                            kljuceviZaBrisanje.Add(par.Key);
                        }
                    }

                    foreach (var k in kljuceviZaBrisanje)
                    {
                        storage.Remove(k);
                        brojObrisanih++;
                    }
                }

                if (brojObrisanih > 0)
                {
                    Evidencija.Upisi($"[CLEANER] Obrisano {brojObrisanih} isteklih elemenata iz kesa.");
                }
            }
        }

        public string DohvatiIliPreuzmi(string kljuc, Func<string> funkcijaZaPreuzimanje)
        {
            CacheItem item;

            lock (zakljucavanje)
            {
                if (!storage.TryGetValue(kljuc, out item))
                {
                    item = new CacheItem();
                    storage[kljuc] = item;
                    Evidencija.Upisi($"[CACHE MISS] Kreiran novi unos za : {kljuc}");
                }
            }

            lock (item)
            {
                if (item.podaci != null && DateTime.Now < item.vremeIsticanja)
                {
                    Evidencija.Upisi($"[CACHE HIT] Podaci pronadjeni za : {kljuc}");
                    return item.podaci;
                }

                if (item.preuzimanjeUToku)
                {
                    Evidencija.Upisi($"[STAMPEDE PREVENTION] Nit ceka na rezultat za : {kljuc}");
                    while (item.preuzimanjeUToku)
                    {
                        Monitor.Wait(item);
                    }
                    return item.podaci;
                }

                item.preuzimanjeUToku = true;
            }

            string rezultatPreuzimanja = null;
            try
            {
                Evidencija.Upisi($"[API FETCH] Preuzimanje sa API-ja za kljuc : {kljuc}");
                rezultatPreuzimanja = funkcijaZaPreuzimanje();
            }
            catch (Exception e)
            {
                Evidencija.Upisi($"[API ERROR] Greska pri dohvatanju podataka : {e.Message}");
                lock (zakljucavanje) { storage.Remove(kljuc); }
                throw;
            }
            finally
            {
                lock (item)
                {
                    if (rezultatPreuzimanja != null)
                    {
                        item.podaci = rezultatPreuzimanja;
                        item.vremeIsticanja = DateTime.Now.Add(trajanjeKesa);
                    }
                    item.preuzimanjeUToku = false;
                    Monitor.PulseAll(item);
                }
            }

            return rezultatPreuzimanja;
        }
    }
}