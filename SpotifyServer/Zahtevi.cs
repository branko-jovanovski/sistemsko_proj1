using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace SpotifyApiServer
{
    public class Zahtevi
    {
        private readonly Queue<HttpListenerContext> red = new Queue<HttpListenerContext>();
        private readonly int maxKapacitet;
        private readonly object zakljucavanje = new object();
        private bool isStopping = false;

        public Zahtevi(int kapacitet = 100)
        {
            maxKapacitet = kapacitet;
        }

        public void DodajURed(HttpListenerContext zahtev)
        {
            lock (zakljucavanje)
            {
                while (red.Count >= maxKapacitet && !isStopping)
                {
                    Evidencija.Upisi("[QUEUE] Red je pun, glavna nit ceka ...");
                    Monitor.Wait(zakljucavanje);
                }

                if (isStopping) return;

                red.Enqueue(zahtev);
                Evidencija.Upisi($"[QUEUE] Zahtev dodat. Trenutni broj : {red.Count}");
                
                Monitor.Pulse(zakljucavanje);
            }
        }

        public HttpListenerContext UzmiIzReda()
        {
            lock (zakljucavanje)
            {
                while (red.Count == 0 && !isStopping)
                {
                    Monitor.Wait(zakljucavanje);
                }

                if (isStopping && red.Count == 0)
                    return null;

                HttpListenerContext zahtev = red.Dequeue();
                
                Monitor.Pulse(zakljucavanje);
                return zahtev;
            }
        }

        public void Stop()
        {
            lock (zakljucavanje)
            {
                isStopping = true;
                Monitor.PulseAll(zakljucavanje);
                Evidencija.Upisi("[QUEUE] Stop signal poslat svim nitima.");
            }
        }
    }
}