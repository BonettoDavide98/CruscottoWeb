using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
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

        MemoryMappedFile[] mmfs = new MemoryMappedFile[MAX_CAMS];

        IPCMessageQueueServer<int> statsQueue = null;

        //coda di messaggi che riceve un oggetto di tipo Settings "serializzato" a stringa con il suo metodo Serialize
        //TODO: refactoring per serializzare in byte[]; non sono riuscito a farlo perchè il BinaryMessageFormatter di IPCMessageQueue dava grane
        IPCMessageQueueServer<List<string>> settingsQueue = null;

        //coda di messaggi che invia comandi al programma dove gira Sherlock o altro; nella tupla il primo string è l'identificatore del comando,
        //ad esempio START, STOP, SET, ecc..., mentre il secondo string contiene gli argomenti da passare
        //TODO: creare una classe al posto che usare tupla? (ma la classe dovrà cambiare da programma a programma)
        IPCMessageQueueClient<List<Tuple<string, string>>> sendQueue = null;

        //inizializza settingsQueue e sendQueue, chiamato da Default.aspx
        //le code contenute nell'array receiveQueues verranno inizializzate quando si riceveranno i Setting
        public void StartMessageQueue()
        {
            if (statsQueue == null)
                statsQueue = new IPCMessageQueueServer<int>("bitmapQueue1", UpdateBitmap);

            if (settingsQueue == null)
                settingsQueue = new IPCMessageQueueServer<List<string>>("settingsQueue", UpdateSettings);
            
            if (sendQueue == null)
                sendQueue = new IPCMessageQueueClient<List<Tuple<string, string>>>("ASPtoProgram");
            sendQueue.Send(new List<Tuple<string, string>> { new Tuple<string, string>("SETUP", "") });
           
        }

        //primo elemento = numero cam da aggiornare
        //secondo elemento = array di byte contenente l'immagine da visualizzare (8 bit per B/N, 32 bit per colore)
        //terzo elemento = contatori TOT, OK, KO
        private void UpdateBitmap(int index)
        {
            int width = ScreenWidth[index];
            int height = ScreenHeight[index];

            byte[] imageArray;

            using (MemoryMappedViewStream stream = mmfs[index].CreateViewStream())
            {
                BinaryReader br = new BinaryReader(stream);
                imageArray = br.ReadBytes((int)stream.Length);
            }

            UpdateStats(imageArray.Length, 0, 0);

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
            Marshal.Copy(imageArray, 0, bmpData.Scan0, imageArray.Length);
            bmp.UnlockBits(bmpData);

            //immagine viene convertita in base64
            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            bmp.Save(ms, ImageFormat.Jpeg);
            byte[] byteImage = ms.ToArray();
            UpdateImage(index, "data:image/png;base64," + Convert.ToBase64String(byteImage));
        }

        //lista di string ricevuta in settingsQueue viene "deserializzata" dal metodo Settings.Deserialize o dal costruttore Settings(List<string>)
        //in oggetto Settings e vengono applicate le impostazioni in essa contenute
        private void UpdateSettings(List<string> data)
        {
            ResetSettings();

            Settings settings = new Settings(data);

            //Dimensioni schermo
            if (settings.Width != 0 && settings.Height != 0)
            {
                if (settings.CamID == -1)
                {
                    for (int i = 0; i < MAX_CAMS; i++)
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
                for (int i = 0; i < MaxCams; i++)
                {
                    try
                    {
                        mmfs[i] = MemoryMappedFile.OpenExisting("img" + i);
                    }
                    catch
                    {
                        throw new Exception();
                    }
                }
            }

            //Attiva solo le prime n telecamere
            if (settings.ActiveCams != 0)
            {
                for (int i = 0; i < settings.ActiveCams; i++)
                {
                    ShowCam(i);
                }
                for (int i = settings.ActiveCams; i < MaxCams; i++)
                {
                    HideCam(i);
                }
            }

            //Definisce se l'immagine è in bianco e nero o a colori
            //se bytePerPixel è == 1 e in B/N, altrimenti è a colori
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

        //comandi di esempio
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
            sendQueue = new IPCMessageQueueClient<List<Tuple<string, string>>>("ASPtoProgram");
            List<Tuple<string, string>> commandList = new List<Tuple<string, string>>();
            commandList.Add(new Tuple<string, string>("SET", parameterName + "/" + value));
            SendCommand(commandList);
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