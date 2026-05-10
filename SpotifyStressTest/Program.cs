using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static readonly string ServerUrl = "http://localhost:8080/search";

    static async Task Main()
    {
        Console.WriteLine("Pritisni ENTER za pocetak stress testa...");
        Console.ReadLine();

        await PokreniTest("TEST 1: Cache Stampede", new[] { "Taylor Swift" }, 50, 50);

        string[] upiti = { "Queen", "Drake", "Eminem", "Rihanna", "Adele", "Metallica" };
        await PokreniTest("TEST 2: Razliciti upiti", upiti, 1000, 100);

        Console.WriteLine("\n=== STRES TEST ZAVRSEN ===");
        Console.ReadLine();
    }

    static async Task PokreniTest(string imeTesta, string[] upiti, int brojZahteva, int maxParalelno)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n{imeTesta}");
        Console.ResetColor();

        var sw = Stopwatch.StartNew();
        var sem = new SemaphoreSlim(maxParalelno);
        var tasks = new List<Task>();
        var rnd = new Random();

        int uspesni = 0;
        int greske = 0;

        for (int i = 0; i < brojZahteva; i++)
        {
            await sem.WaitAsync(); 

            string rec = upiti[rnd.Next(upiti.Length)];
            string url = $"{ServerUrl}?q={rec}";


            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var res = await client.GetAsync(url);
                    
                    if (res.IsSuccessStatusCode){
                        Interlocked.Increment(ref uspesni);
                    }
                    else
                    {
                        Interlocked.Increment(ref greske);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref greske);
                }
                finally
                {
                    sem.Release(); 
                }
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();
        
        
        if (greske == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
        }

        Console.WriteLine($"Vreme: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"Uspesnih: {uspesni}");
        Console.WriteLine($"Gresaka: {greske}");
        Console.ResetColor();
    }
}

