using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace CruscottoWeb
{
    public class Settings
    {
        public int CamID { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int MaxCams { get; set; }
        public int ActiveCams { get; set; }
        public int NumPages { get; set; }
        public int BytesPerPixel { get; set; }
        public List<string> ParameterNames { get; set; }

        public Settings(int camID, int width, int height, int maxCams, int numPages, int bytesPerPixel)
        {
            CamID = camID;
            Width = width;
            Height = height;
            MaxCams = maxCams;
            NumPages = numPages;
            BytesPerPixel = bytesPerPixel;
        }

        public Settings(List<string> serializedData)
        {
            Deserialize(serializedData);
        }

        public void AddParameterName(string parameterName)
        {
            ParameterNames.Add(parameterName);
        }

        public List<string> Serialize()
        {
            List<string> data = new List<string>();

            data.Add(CamID.ToString());
            data.Add(Width.ToString());
            data.Add(Height.ToString());
            data.Add(MaxCams.ToString());
            data.Add(ActiveCams.ToString());
            data.Add(NumPages.ToString());
            data.Add(BytesPerPixel.ToString());
            if (ParameterNames != null)
                foreach (string parameterName in ParameterNames)
                    data.Add(parameterName.ToString());

            return data;
        }

        public void Deserialize(List<string> data)
        {
            CamID = Int16.Parse(data[0]);
            Width = Int16.Parse(data[1]);
            Height = Int16.Parse(data[2]);
            MaxCams = Int16.Parse(data[3]);
            ActiveCams = Int16.Parse(data[4]);
            NumPages = Int16.Parse(data[5]);
            BytesPerPixel = Int16.Parse(data[6]);
            for (int i = 7; i < data.Count; i++)
            {
                ParameterNames.Add(data[i]);
            }
        }
    }
}