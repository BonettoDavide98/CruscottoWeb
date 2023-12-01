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
        const int MAX_CAMS = 11;

        int[] ScreenWidth = new int[MAX_CAMS];
        int[] ScreenHeight = new int[MAX_CAMS];

        List<string> parameterNames = new List<string>();
        
        int MaxCams = 1;
        int bytePerPixel = 1;

        IPCMessageQueueServer<Tuple<int, byte[], int[]>>[] receiveQueues = null;
        IPCMessageQueueServer<List<string>> settingsQueue = null;
        IPCMessageQueueClient<List<Tuple<string, string>>> sendQueue = null;

        public void StartMessageQueue()
        {
            if(receiveQueues == null)
                receiveQueues = new IPCMessageQueueServer<Tuple<int, byte[], int[]>>[MAX_CAMS];

            if(settingsQueue == null)
                settingsQueue = new IPCMessageQueueServer<List<string>>("settingsQueue", UpdateSettings);
        }

        //primo elemento = numero cam da aggiornare
        //secondo elemento = array di byte contenente l'immagine da visualizzare (8 bit per B/N, 32 bit per colore)
        //terzo elemento = contatori TOT, OK, KO
        private void UpdateBitmap(Tuple<int, byte[], int[]> tupla)
        {
            int width = ScreenWidth[tupla.Item1];
            int height = ScreenHeight[tupla.Item1];

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

            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            bmp.Save(ms, ImageFormat.Jpeg);
            byte[] byteImage = ms.ToArray();
            UpdateImage(tupla.Item1, "data:image/png;base64," + Convert.ToBase64String(byteImage));
            UpdateStats(tupla.Item3[0], tupla.Item3[1], tupla.Item3[2]);
        }

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


        private void SendCommand(List<Tuple<string, string>> commandList)
        {
            sendQueue.Send(commandList);
        }

        public void Stop()
        {
            if(sendQueue == null)
                sendQueue = new IPCMessageQueueClient<List<Tuple<string, string>>>("ASPtoProgram");
            List<Tuple<string, string>> commandList = new List<Tuple<string, string>>();
            commandList.Add(new Tuple<string, string>("STOP", ""));
            SendCommand(commandList);
        }

        public void Start()
        {
            if (sendQueue == null)
                sendQueue = new IPCMessageQueueClient<List<Tuple<string, string>>>("ASPtoProgram");
            List<Tuple<string, string>> commandList = new List<Tuple<string, string>>();
            commandList.Add(new Tuple<string, string>("START", ""));
            SendCommand(commandList);
        }

        public void Set(string parameterName, string value)
        {
            if (sendQueue == null)
                sendQueue = new IPCMessageQueueClient<List<Tuple<string, string>>>("ASPtoProgram");
            List<Tuple<string, string>> commandList = new List<Tuple<string, string>>();
            commandList.Add(new Tuple<string, string>("SET", parameterName + "/" + value));
            SendCommand(commandList);
        }

        public void ChangePage(string page)
        {
            if (sendQueue == null)
                sendQueue = new IPCMessageQueueClient<List<Tuple<string, string>>>("ASPtoProgram");
            List<Tuple<string, string>> commandList = new List<Tuple<string, string>>();
            commandList.Add(new Tuple<string, string>("PAGE", page));
            SendCommand(commandList);
        }
    }
}