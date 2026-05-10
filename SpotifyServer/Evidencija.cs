namespace SpotifyApiServer
{
    public static class Evidencija
    {
        private static readonly object zakljucavanjeFajla =  new object();

        private static readonly string putanjaFajla = Path.Combine(Directory.GetCurrentDirectory(), "server_activity.txt");

        public static void Upisi(string poruka)
        {
           if (string.IsNullOrWhiteSpace(poruka))
            {
                return;
            } 

                string vreme = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                int idNiti = Thread.CurrentThread.ManagedThreadId;

                string tekst = $"[{vreme}] [Nit ID: {idNiti:D2}] {poruka}";

            try
            {
                lock (zakljucavanjeFajla)
                {
                    Console.WriteLine(tekst);
                    File.AppendAllText(putanjaFajla , tekst + Environment.NewLine);
                }
            }
            catch(Exception e)
            {
                lock (zakljucavanjeFajla){

                    Console.ForegroundColor = ConsoleColor.Red;

                    Console.WriteLine($"[{vreme}] ERROR (Nit : {idNiti:D2}) : Greska pri upisu u fajl : {e.Message}");
                    
                    Console.ResetColor();
                }

            }   
        }
    }
}



