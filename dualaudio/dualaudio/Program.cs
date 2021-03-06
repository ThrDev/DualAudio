﻿using System;
using System.Collections.Generic;
using System.Text;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.IO;
using System.Threading;
using System.IO.Compression;
using System.Reflection;
namespace dualaudio
{
    class Program
    {
        static WasapiLoopbackCapture waveIn;
        static BufferedWaveProvider[] m1;
        static int totaloutputs = 0;
        static bool dumptofile = false;
        static WaveFileWriter f;
        public static void Main()
        {
            //Decrypt resources and load.
            Console.WriteLine("---------------------------------------------------------------------");
            Console.WriteLine("Dual Audio - (C) 2014 Thr. Using NAudio (http://naudio.codeplex.com/)");
            Console.WriteLine("---------------------------------------------------------------------");

            int waveInDevices = WaveIn.DeviceCount;
            int waveOutDevices = WaveOut.DeviceCount;
            int inputdevice = 0;
            int output = 0;

            List<int> Outputs = new List<int>();
            Console.WriteLine("Located {0} Input Devices.\n", waveInDevices);
            Console.Write("How many outputs to bind to? (max {0}): ", waveOutDevices);
            //grab inputs.
            
            while (!int.TryParse(Console.ReadLine(), out totaloutputs) || totaloutputs > waveOutDevices)
                Console.Write("How many outputs to bind to? (max {0}): ", waveOutDevices);

            for (int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++)
            {
                WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(waveInDevice);
                Console.WriteLine("{0}: {1}, {2} channels.", waveInDevice, deviceInfo.ProductName, deviceInfo.Channels);
            }

            Console.Write("Select Input Line: ");           
            while (!int.TryParse(Console.ReadLine(), out inputdevice))
                Console.Write("Select Input Line: ");

            Console.WriteLine("Successfully set input as device {0}.", inputdevice);
            Console.WriteLine("");
            output = totaloutputs;
            while (output > 0)
            {
                for (int waveOutDevice = 0; waveOutDevice < waveOutDevices; waveOutDevice++)
                {
                    if (!Outputs.Contains(waveOutDevice))
                    {
                        WaveOutCapabilities deviceInfo = WaveOut.GetCapabilities(waveOutDevice);
                        Console.WriteLine("{0}: {1}, {2}", waveOutDevice, deviceInfo.ProductName, deviceInfo.Channels);
                    }
                }
                Console.Write("Select the output device for playback{0}: ", (totaloutputs - output).ToString());
                int device = 0;
                while(!int.TryParse(Console.ReadLine(), out device) || device > waveOutDevices - 1)
                {
                    Console.WriteLine("Invalid Device!");
                    Console.Write("Select the output device for playback{0}: ", (totaloutputs - output).ToString());
                }
                Outputs.Add(device);
                Console.WriteLine("Successfully set the output device for playback{0}.", (totaloutputs - output).ToString());
                output--;
            }
            Console.WriteLine("");
            string p = "";
            Console.Write("Dump to file? (Y\\N) ");
            while((p = Console.ReadLine().ToLower()) != "y" && p != "n")
            {
                Console.WriteLine("");
                Console.Write("Dump to file? (Y\\N) ");
            }
            dumptofile = Convert.ToBoolean(p == "y" ? true : false);

            Console.Write("Amplify Output? (Y\\N) ");
            while ((p = Console.ReadLine().ToLower()) != "y" && p != "n")
            {
                Console.WriteLine("");
                Console.Write("Amplify Output? (Y\\N) ");
            }
            amplify = Convert.ToBoolean(p == "y" ? true : false);
           
            waveIn = new WasapiLoopbackCapture();

            Console.WriteLine("Initialized Loopback Capture...");
            if (dumptofile)
            {
                string filename = "";
                Console.Write("Filename (without extension): ");
                while((filename = Console.ReadLine()) == "")
                {
                    Console.WriteLine("");
                    Console.Write("Filename (without extension): ");
                }
                f = new WaveFileWriter(File.OpenWrite(Environment.CurrentDirectory + "\\" + filename + ".wav"), waveIn.WaveFormat);
            }
            waveIn.DataAvailable += InputBufferToFileCallback;
            waveIn.StartRecording(); //Start our loopback capture.
            
            WaveOut[] devices = new WaveOut[totaloutputs];

            m1 = new BufferedWaveProvider[totaloutputs];
            for (int i = 0; i < totaloutputs; i++)
            {
                m1[i] = new BufferedWaveProvider(waveIn.WaveFormat);
                m1[i].BufferLength = 1024 * 1024 * 10;
                m1[i].DiscardOnBufferOverflow = true;
                devices[i] = new WaveOut();
                devices[i].Volume = 3;
                devices[i].NumberOfBuffers = 3;
                devices[i].DeviceNumber = Outputs[i];
                devices[i].DesiredLatency = 61;
                devices[i].Init(m1[i]);
                Console.WriteLine("Initializing Device{0}...", i);
                devices[i].Play();
                Console.WriteLine("Started Playing on Device{0}...", i);
            }

            while (true)
                if(Console.ReadLine().ToLower() == "s")
                {
                    stop = true;
                    for (int i = 0; i < devices.Length; i++)
                        devices[i].Stop();
                    waveIn.StopRecording();
                    f.Close();
                    Environment.Exit(0);
                }
        }

        private static bool stop = false;
        private static bool amplify = false;
        private static int amplification = 10;
        private static void InputBufferToFileCallback(object sender, WaveInEventArgs e)
        {
            //write to our audio sample buffers.
            if (!stop)
            {
                if (amplify)
                {
                    var erg = new byte[e.BytesRecorded];
                    for (int i = 0; i < e.BytesRecorded; i += 2)
                    {
                        var sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                        erg[i] = (byte)((sample * amplification) & 0xff);
                        erg[i + 1] = (byte)(((sample * amplification) >> 8) & 0xff);
                    }
                    for (int i = 0; i < totaloutputs; i++)
                        m1[i].AddSamples(erg, 0, e.BytesRecorded);
                }
                else
                {
                    for (int i = 0; i < totaloutputs; i++)
                        m1[i].AddSamples(e.Buffer, 0, e.BytesRecorded);
                }
                if (dumptofile && f.CanWrite)
                {
                    f.Write(e.Buffer, 0, e.BytesRecorded);
                }
            }
        }
    }
}
