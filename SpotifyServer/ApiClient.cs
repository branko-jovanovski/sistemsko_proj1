using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DotNetEnv;


namespace SpotifyApiServer
{
    public class SpotifyApiClient
    {
        private string clientId;
        private string clientSecret;
        
        private string accessToken = null;
        private DateTime tokenExpiry = DateTime.MinValue;
        private readonly object tokenLock = new object();

        private readonly HttpClient _httpClient = new HttpClient();

        private void EnsureValidToken()
        {
            
            if (DateTime.Now < tokenExpiry) return;

            lock (tokenLock)
            {
                Env.Load();
                
                clientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");
                clientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET");

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    throw new InvalidOperationException("Kriticna greska: SPOTIFY_CLIENT_ID ili SPOTIFY_CLIENT_SECRET nisu pronadjeni u Environment promenljivama!");
                }

                if (DateTime.Now >= tokenExpiry)
                {
                    Evidencija.Upisi("[API] Preuzimanje novog Spotify Access tokena...");
                    
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
                    var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
                    
                    request.Content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "client_credentials")
                    });

                    var response = _httpClient.Send(request); 
                    response.EnsureSuccessStatusCode();
                    
                    var jsonString = new StreamReader(response.Content.ReadAsStream()).ReadToEnd();
                    using var doc = JsonDocument.Parse(jsonString);
                    
                    accessToken = doc.RootElement.GetProperty("access_token").GetString();
                    int expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
                    
                    tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60); 
                    
                    Evidencija.Upisi("[API] Token uspesno osvezen!");
                }
            }
        }

        public string Search(string query, string type, string limit)
        {
            EnsureValidToken();

            string url = $"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(query)}&type={type}&limit={limit}";
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = _httpClient.Send(request);
            
            if (!response.IsSuccessStatusCode)
            {
                string detalji = new StreamReader(response.Content.ReadAsStream()).ReadToEnd();
                Evidencija.Upisi($"[SPOTIFY ERROR] {detalji}");
                throw new Exception($"Spotify API error: {response.StatusCode}");
            }

            using var reader = new StreamReader(response.Content.ReadAsStream());
            string jsonString = reader.ReadToEnd();

            using var doc = JsonDocument.Parse(jsonString);
            string rootNode = type + "s"; 
            
            if (doc.RootElement.TryGetProperty(rootNode, out JsonElement nodeProperty))
            {
                if (nodeProperty.GetProperty("items").GetArrayLength() == 0)
                {
                    throw new KeyNotFoundException($"Trazena pesma ili album '{query}' ne postoji na Spotify-ju."); 
                }
            }

            return jsonString;
        }
    }
}

