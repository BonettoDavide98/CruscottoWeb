using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web;
using Microsoft.AspNet.SignalR;
using Qualivision.InterprocessCommunication;

namespace CruscottoWeb
{
    public class HubMessageQueue : Hub
    {
        //Numero massimo telecamere in contemporanea sullo schermo, fare attenzione alla risoluzione
        const int MAX_CAMS = 2;

        //arrays contenenti le risoluzioni di ogni telecamera, solitamente sono tutte uguali
        int[] ScreenWidth = new int[MAX_CAMS];
        int[] ScreenHeight = new int[MAX_CAMS];

        //lista contenente nomi di vari parametri
        List<string> parameterNames = new List<string>();
        
        int MaxCams = 1;
        int bytePerPixel = 1;

        //code di messaggi MSMQ

        //array di code di ricevimento dati; nella tupla il primo int è l'ID della telecamera, byte[] è l'array dell'immagine (MASSIMO CIRCA 4000 kb)
        //e int[] è un array che contiene le statistiche TOT, OK, KO
        IPCMessageQueueServer<Tuple<int, byte[], int[]>>[] receiveQueues = null;

        //coda di messaggi che riceve un oggetto di tipo Settings "serializzato" a stringa con il suo metodo Serialize
        //TODO: refactoring per serializzare in byte[]; non sono riuscito a farlo perchè il BinaryMessageFormatter di IPCMessageQueue dava eccezioni
        IPCMessageQueueServer<List<string>> settingsQueue = null;

        //coda di messaggi che invia comandi al programma dove gira Sherlock o altro; nella tupla il primo string è l'identificatore del comando,
        //ad esempio START, STOP, SET, ecc..., mentre il secondo string contiene gli argomenti da passare
        //TODO: forse creare una classe al posto che usare tupla? (ma la classe cambia da programma a programma)
        IPCMessageQueueClient<List<Tuple<string, string>>> sendQueue = null;

        //inizializza settingsQueue e sendQueue, chiamato da Default.aspx
        //le code contenute nell'array receiveQueues verranno inizializzate quando si riceveranno i Setting
        public void StartMessageQueue()
        {
            if(receiveQueues == null)
                receiveQueues = new IPCMessageQueueServer<Tuple<int, byte[], int[]>>[MAX_CAMS];

            if(settingsQueue == null)
                settingsQueue = new IPCMessageQueueServer<List<string>>("settingsQueue", UpdateSettings);
            
            if (sendQueue == null)
                sendQueue = new IPCMessageQueueClient<List<Tuple<string, string>>>("ASPtoProgram");
        }

        //primo elemento = numero cam da aggiornare
        //secondo elemento = array di byte contenente l'immagine da visualizzare (8 bit per B/N, 32 bit per colore)
        //terzo elemento = contatori TOT, OK, KO
        private void UpdateBitmap(Tuple<int, byte[], int[]> tupla)
        {
            int width = ScreenWidth[tupla.Item1];
            int height = ScreenHeight[tupla.Item1];

            //algoritmo cambia se l'immagine è a colori o in bianco e nero
            Bitmap bmp = new Bitmap(width, height, (bytePerPixel == 1) ? PixelFormat.Format8bppIndexed : PixelFormat.Format32bppRgb);
            if (bytePerPixel == 1)
            {
                ColorPalette pal = bmp.Palette;
                Color[] entries = pal.Entries;
                for (int i = 0; i < 255; i++)
                    entries[i] = Color.FromArgb(i, i, i);
                bmp.Palette = pal;
            }
            BitmapData bmpData = bmp.LockBits(
                                 new Rectangle(0, 0, bmp.Width, bmp.Height),
                                 ImageLockMode.WriteOnly, bmp.PixelFormat);
            Marshal.Copy(tupla.Item2, 0, bmpData.Scan0, tupla.Item2.Length);
            bmp.UnlockBits(bmpData);

            //immagine viene convertita in base64
            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            bmp.Save(ms, ImageFormat.Jpeg);
            byte[] byteImage = ms.ToArray();
            UpdateImage(tupla.Item1, "data:image/png;base64," + Convert.ToBase64String(byteImage));
            UpdateStats(tupla.Item3[0], tupla.Item3[1], tupla.Item3[2]);
        }

        //lista di string ricevuta in settingsQueue viene "deserializzata" in oggetto Settings e vengono applicate le impostazioni in essa contenute
        private void UpdateSettings(List<string> data)
        {
            ResetSettings();

            Settings settings = new Settings(data);

            //Dimensioni schermo
            if (settings.Width != 0 && settings.Height != 0)
            {
                if (settings.CamID == 0)
                {
                    for (int i = 1; i < 11; i++)
                    {
                        ScreenWidth[i] = settings.Width;
                        ScreenHeight[i] = settings.Height;
                    }
                }
                else
                {
                    ScreenWidth[settings.CamID] = settings.Width;
                    ScreenHeight[settings.CamID] = settings.Height;
                }
            }

            //Numero pagine
            if (settings.NumPages != 0)
            {
                SetPages(settings.NumPages);
            }

            //Numero massimo di telecamere in contemporanea sullo schermo
            if (settings.MaxCams != 0)
            {
                MaxCams = settings.MaxCams;

                if (MaxCams > MAX_CAMS)
                    MaxCams = MAX_CAMS;

                SetCams(MaxCams);
                for (int i = 1; i <= MaxCams; i++)
                {
                    receiveQueues[i] = new IPCMessageQueueServer<Tuple<int, byte[], int[]>>("bitmapQueue" + i, UpdateBitmap);
                }
            }

            //Attiva solo le prime n telecamere
            if (settings.ActiveCams != 0)
            {
                for (int i = 1; i <= settings.ActiveCams; i++)
                {
                    ShowCam(i);
                }
                for (int i = settings.ActiveCams; i < MaxCams; i++)
                {
                    HideCam(i);
                }
            }

            //Definisce se l'immagine è in bianco e nero o a colori
            if (settings.BytesPerPixel != 0)
            {
                bytePerPixel = settings.BytesPerPixel;
            }

            //importa nomi parametri modificabili
            if(settings.getParameters() != null)
            {
                parameterNames = settings.getParameters();
                SetParameters(parameterNames);
            }
        }

        //CLIENT BROADCASTS
        //metodi che vanno a richiamare i metodi corrispondenti in ogni client (si veda il Javascript in Default.aspx)
        #region client broadcasts
        private void UpdateStats(int tot, int ok, int ko)
        {
            Clients.All.UpdateStats(tot, ok, ko);
        }

        private void ShowCam(int cam)
        {
            Clients.All.ShowCam(cam);
        }

        private void HideCam(int cam)
        {
            Clients.All.HideCam(cam);
        }

        private void SetPages(int pages)
        {
            Clients.All.SetPages(pages);
        }

        private void SetCams(int cams)
        {
            Clients.All.SetCams(cams);
        }

        private void UpdateImage(int camNumber, string Base64Data)
        {
            Clients.All.UpdateImage(camNumber, Base64Data);
        }

        private void SetParameters(List<string> parameterNames)
        {
            foreach(string parameterName in parameterNames)
            {
                Clients.All.AddParameter(parameterName);
            }
        }

        private void ResetSettings()
        {
            Clients.All.ResetSettings();
        }
        #endregion client broadcasts
        
        //metodo generale da chiamare per inviare un comando
        private void SendCommand(List<Tuple<string, string>> commandList)
        {
            sendQueue.Send(commandList);
        }

        //inizio comandi di esempio
        public void Stop()
        {
            sendQueue = new IPCMessageQueueClient<List<Tuple<string, string>>>("ASPtoProgram");
            List<Tuple<string, string>> commandList = new List<Tuple<string, string>>();
            commandList.Add(new Tuple<string, string>("STOP", ""));
            SendCommand(commandList);
        }

        public void Start()
        {
            sendQueue = new IPCMessageQueueClient<List<Tuple<string, string>>>("ASPtoProgram");
            List<Tuple<string, string>> commandList = new List<Tuple<string, string>>();
            commandList.Add(new Tuple<string, string>("START", ""));
            SendCommand(commandList);
        }

        public void Set(string parameterName, string value)
        {
            sendQueue.Send(new List<Tuple<string, string>> { new Tuple<string, string>("SETUP", "") });
            //sendQueue = new IPCMessageQueueClient<List<Tuple<string, string>>>("ASPtoProgram");
            //List<Tuple<string, string>> commandList = new List<Tuple<string, string>>();
            //commandList.Add(new Tuple<string, string>("SET", parameterName + "/" + value));
            //SendCommand(commandList);
        }

        public void ChangePage(string page)
        {
            sendQueue = new IPCMessageQueueClient<List<Tuple<string, string>>>("ASPtoProgram");
            List<Tuple<string, string>> commandList = new List<Tuple<string, string>>();
            commandList.Add(new Tuple<string, string>("PAGE", page));
            SendCommand(commandList);
        }
        //fine comandi di esempio
    }
}