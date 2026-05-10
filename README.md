# Spotify Search API

## Opis projekta
Ovaj projekat predstavlja serversku aplikaciju realizovanu kao konzolni program u jeziku C#. Sistem funkcioniše kao prilagođeni Web server (korišćenjem `HttpListener` klase) koji klijentima omogućava pretragu pesama i albuma integracijom sa Spotify Web API-jem.

Glavni fokus projekta je na konkurentnom programiranju, pravilnoj sinhronizaciji niti i upravljanju deljenim resursima u uslovima visokog opterećenja.

---

## Arhitektura sistema
Sistem je dizajniran na osnovu Producer-Consumer (Proizvođač-Potrošač) principa, čime je postignuto striktno razdvajanje prijema i obrade zahteva:

*   **Nit za prijem (Slušač):** Glavna nit prihvata dolazne HTTP zahteve i smešta ih u deljenu `Queue` strukturu (red) ograničenog kapaciteta.
*   **Radne niti (Worker Threads):** Skup paralelnih niti kontinuirano preuzima zahteve iz reda i obrađuje ih.
*   **Pristup resursima:** Koristi se thread-safe keš koji pamti rezultate pretrage. Ako više niti traži istu pesmu, podaci se preuzimaju samo jednom sa interneta, dok sve ostale niti bezbedno čekaju i koriste taj isti rezultat.

---

## Upravljanje memorijom (TTL Keš)
Implementirana je strategija aktivnog čišćenja. Pozadinska nit `Nit-CistacKesa` se budi periodično (svakih 20s) i uklanja elemente kojima je isteklo vreme trajanja. Ovo osigurava da memorija servera ostane optimizovana čak i pri dugotrajnom radu.

---

## Pokretanje i Testiranje

### 1. Priprema kredencijala
Kreirajte `.env` fajl u korenskom direktorijumu projekta i unesite svoje podatke:

SPOTIFY_CLIENT_ID=""

SPOTIFY_CLIENT_SECRET=""

### 2. Pokretanje servera
Projekat se pokreće iz terminala (Visual Studio Code) komandom:

 ```bash
dotnet run
```
Aplikacija će se pokrenuti i slušati na adresi: http://localhost:8080/

### 3. Format API Zahteva

Preporučeno je korišćenje alata Postman ili običnog web pregledača.
 ```bash
http://localhost:8080/search?q=Taylor+Swift&type=track&limit=5
```







## Stres testiranje i validacija sistema

Za potrebe verifikacije stabilnosti i performansi servera, razvijen je namenski Stress Tester (konzolna aplikacija). Ovaj alat simulira ekstremne uslove rada kako bi se potvrdila ispravnost mehanizama sinhronizacije.

### Metodologija testiranja
Tester koristi napredne tehnike konkurentnosti u C#-u:
*   **SemaphoreSlim:** Kontroliše maksimalni broj istovremenih konekcija ka serveru (istovremeno leti 50 ili 100 zahteva).
*   **Interlocked.Increment:** Obezbeđuje atomsko i thread-safe brojanje uspešnih i neuspešnih odgovora.
*   **Task.WhenAll:** Omogućava asinhrono čekanje na završetak svih generisanih zadataka.

### Kako pokrenuti test:
1.  Pokrenite glavni server (`SpotifyApiServer`).
2.  U drugom terminalu pokrenite tester:
    ```bash
    dotnet run
    ```

### Tumačenje rezultata
*   **Zeleni ispis:** Test je prošao bez ijedne greške (Status 200 OK za sve zahteve).
*   **Crveni ispis:** Došlo je do grešaka u komunikaciji (npr. server je odbio konekciju ili je Spotify API vratio grešku zbog prevelikog broja zahteva u kratkom periodu).
*   **Vreme (ms):** Ukupno vreme potrebno da server obradi svih 1000 zahteva.
