using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Diagnostics; 

namespace SpotifyApiServer
{
    class Program
    {
        private static readonly Zahtevi _zahtevi = new Zahtevi(50);
        private static readonly Cache _cache = new Cache(TimeSpan.FromSeconds(20));
        private static readonly SpotifyApiClient _spotifyClient = new SpotifyApiClient();
        
        private static readonly int brojRadnihNiti = 5; 
        private static bool _isRunning = true;

        private static readonly bool koristiSistemskiThreadPool = false; 

        static void Main(string[] args)
        {
            Evidencija.Upisi("Pokretanje servera...");

            if (!koristiSistemskiThreadPool)
            {
                Evidencija.Upisi("[REZIM] Koriste se sopstvene niti.");
                for (int i = 0; i < brojRadnihNiti; i++)
                {
                    Thread worker = new Thread(ProcessRequestsLoop)
                    {
                        IsBackground = true,
                        Name = $"radnaNit-{i + 1}"
                    };
                    worker.Start();
                }
            }
            else
            {
                Evidencija.Upisi("[REZIM] Koristi se ThreadPool.");
                ThreadPool.SetMaxThreads(brojRadnihNiti, brojRadnihNiti);
            }

            using HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/");
            
            try
            {
                listener.Start(); 
            }
            catch (Exception e)
            {
                Evidencija.Upisi($"[FATAL] Greska pri pokretanju servera : {e.Message}");
                return;
            }
            
            Evidencija.Upisi("Server slusa na http://localhost:8080/");
            Evidencija.Upisi("Primer upita: http://localhost:8080/search?q=Taylor+Swift&type=track&limit=5");
            Console.WriteLine("Klikni bilo koje dugme za gasenje ...");

            Thread shutdownThread = new Thread(() => WaitForShutdown(listener));
            shutdownThread.IsBackground = true; 
            shutdownThread.Start();

            while (_isRunning)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    Evidencija.Upisi($"Zahtev primljen od : {context.Request.RemoteEndPoint}");
                        
                    _zahtevi.DodajURed(context);

                    if (koristiSistemskiThreadPool)
                    {
                        ThreadPool.QueueUserWorkItem(state => {
                            var ctx = _zahtevi.UzmiIzReda();
                            if (ctx != null){
                                ObradiPojedinacniZahtev(ctx);
                            }
                        });
                    }
                }
                catch (Exception e)
                {
                    if (_isRunning) Evidencija.Upisi($"Greska pri prijemu zahteva : {e.Message}");
                }
            }
        }
        
        private static void WaitForShutdown(HttpListener listener)
        {
            Console.ReadKey(); 
            Evidencija.Upisi("Gasenje servera ...");
            _isRunning = false;
            listener.Stop(); 
            _zahtevi.Stop(); 
            Evidencija.Upisi("Server ugasen.");
        }

        private static void ProcessRequestsLoop()
        {
            while (_isRunning)
            {
                HttpListenerContext context = _zahtevi.UzmiIzReda();
                if (context == null){
                    break;
                } 
                ObradiPojedinacniZahtev(context);
            }
        }

        private static void ObradiPojedinacniZahtev(HttpListenerContext context)
        {
            
            Stopwatch stoperica = Stopwatch.StartNew();

            try
            {
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/search")
                {
                    string q = request.QueryString["q"];
                    string type = request.QueryString["type"] ?? "track";
                    string limit = request.QueryString["limit"] ?? "5";

                    if (string.IsNullOrWhiteSpace(q))
                    {
                        SendResponse(response, 400, "{\"error\": \"Nedostaje parametar 'q'\"}");
                        return;
                    }

                    string cacheKey = $"{q}_{type}_{limit}".ToLower();

                    string resultJson = _cache.DohvatiIliPreuzmi(cacheKey, () => 
                    {
                        return _spotifyClient.Search(q, type, limit);
                    });

                    SendResponse(response, 200, resultJson);
                }
                else
                {
                    SendResponse(response, 404, "{\"error\": \"Ruta nije pronadjena. Koristite /search\"}");
                }
            }
            catch (Exception e)
            {
                Evidencija.Upisi($"Greska pri obradi zahteva: {e.Message}");
                if (e.Message.Contains("ne postoji")){
                    SendResponse(context.Response, 404, $"{{\"error\": \"{e.Message}\"}}");
                }
                else{
                    SendResponse(context.Response, 500, "{\"error\": \"Interna greska servera\"}");
                }
            }
            finally
            {
                stoperica.Stop();
                Evidencija.Upisi($"[STATISTIKA] Obrada zavrsena za: {stoperica.ElapsedMilliseconds} ms");
            }
        }

        private static void SendResponse(HttpListenerResponse response, int statusCode, string responseBody)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
                response.StatusCode = statusCode;
                response.ContentType = "application/json";
                response.ContentLength64 = buffer.Length;
                using Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                response.Close();
            }
            catch (Exception e)
            { 
                Evidencija.Upisi($"Greska pri slanju: {e.Message}"); 
            }
        }
    }
}