# CruscottoWeb
Utilizza ASP.NET e SignalR per permettere a utenti nella stessa LAN di interfacciarsi con il programma di visione anche da altri computer tramite webpage.
# Utilizzo
Per interfacciarsi con il cruscotto web un programma deve implementare la classe IPCMessageQueue.cs nei seguenti modi:
- una coda in entrata IPCMessageQueueServer<List<Tuple<string, string>> che riceverà i comandi dal cruscotto (che andranno poi interpretati ed implementati a discrezione del programmatore)
- una coda in uscita IPCMessageQueueClient<List<string>> per inviare impostazioni; la lista di stringhe è la "serializzazione" della classe Settings.cs (se si vuole si potrà in futuro fare un refactoring e passare ad altri tipi di serializzazione)
- n code in uscita IPCMessageQueueClient<Tuple<int, byte[], int[]>>, ognuna delle quali invia un fotogramma rappresentato come byte[] e le statistiche OK/KO in quell'istante rappresentate da int[]
