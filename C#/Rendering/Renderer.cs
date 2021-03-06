﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Numerics;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using OpenCL.Net;

namespace Raymarcher.Rendering
{
    internal class Renderer
    {
        private static Device UsedDevice { get; set; }

        private static CommandQueue Queue { get; set; }

        private static Mem memInput { get; set; }
        private static Mem memOutput;
        private static Mem memVolume { get; set; }

        private static IntPtr memory;

        private static Stopwatch sw = new Stopwatch();

        //private static byte[] entryPixels = new byte[2560 * 1080 * 4];

        private static int inputSize = 0;
        private static int outputSize = 0;
        private static int volumeSize = 0;
        [EngineInitializer(322)]
        public static void Initialize()
        {
            Platform[] platforms = Cl.GetPlatformIDs(out ErrorCode error);
            if (error != ErrorCode.Success)
            {
                Log.Print("Impossible to run OpenCL, no any graphic platform available, abording launch.");

                Application.Exit();
            }

            Vector2I res = Graphics.RenderResolution;
            int pixelAmount = res.x * res.y;

            int amountOfObjects = 1;

            unsafe
            {
                inputSize = sizeof(C_CAMERA);
                outputSize = sizeof(byte) * res.x * res.y * 4;

            }

            UsedDevice = Cl.GetDeviceIDs(platforms[0], DeviceType.All, out error)[0];
            gpu_context = Cl.CreateContext(null, 1, new Device[] { UsedDevice }, null, IntPtr.Zero, out error);
            InfoBuffer namebuffer = Cl.GetDeviceInfo(UsedDevice, DeviceInfo.Name, out error);
            Log.Print("OpenCL Running on " + namebuffer);

            Queue = Cl.CreateCommandQueue(gpu_context, UsedDevice, CommandQueueProperties.OutOfOrderExecModeEnable, out error);
            if (error != ErrorCode.Success)
            {
                Console.WriteLine("Impossible to create gpu queue, abording launch.");

                Application.Exit();
            }

            CLoader.LoadProjectPaths(@".\libs", new[] { "c" }, out string[] cfiles, out string[] hfiles);
            Program program = CLoader.LoadProgram(cfiles, hfiles, UsedDevice, gpu_context);

            //Program prog = CLoader.LoadProgram(CLoader.GetCFilesDir(@".\", new[] { "cl" }).ToArray(), new[] { "headers" }, UsedDevice, gpu_context); 
            kernel = Cl.CreateKernel(program, "rm_render_entry", out error);

            if(error != ErrorCode.Success)
            {
                Log.Print("Error when creating kernel: " + error.ToString());
            }

            memory = new IntPtr(outputSize);
            memInput = (Mem)Cl.CreateBuffer(gpu_context, MemFlags.ReadOnly, inputSize, out error);
            memOutput = (Mem)Cl.CreateBuffer(gpu_context, MemFlags.WriteOnly, outputSize, out error);

            

            //GPU_PARAM param = new GPU_PARAM() { X_RESOLUTION = res.x, Y_RESOLUTION = res.y };

            ////Vector3D pos = camera.Malleable.Position;
            //Quaternion q = camera.Malleable.Rotation;


            

            IntPtr notused;
            InfoBuffer local = new InfoBuffer(new IntPtr(4));
            error = Cl.GetKernelWorkGroupInfo(kernel, UsedDevice, KernelWorkGroupInfo.WorkGroupSize, new IntPtr(sizeof(int)), local, out notused);
            if (error != ErrorCode.Success)
            {
                Log.Print("Error getting kernel workgroup info: " + error.ToString());
            }

            //int intPtrSize = 0;
            intPtrSize = Marshal.SizeOf(typeof(IntPtr));
            
            Cl.SetKernelArg(kernel, 0, (IntPtr)intPtrSize, memInput);
            //Cl.SetKernelArg(kernel, 1, (IntPtr)intPtrSize, memVolume);
            Cl.SetKernelArg(kernel, 3, (IntPtr)intPtrSize, memOutput);

            //Cl.SetKernelArg(kernel, 2, new IntPtr(4), pixelAmount * 4);
            workGroupSizePtr = new IntPtr[] { new IntPtr(pixelAmount) };

            
        }
        private static Context gpu_context;
        private static int intPtrSize = 0;
        private static Kernel kernel;
        private static IntPtr[] workGroupSizePtr;
        private static Stopwatch bakeSW = new Stopwatch();
        internal static double RenderTime = 0.0D;

        static Quaternion rot = EQuaternion.FromEuler(0D, 0D, 0D);
        static Vector3D axis = Vector3D.Up;
        static double rotationSpeed = 20.0D;
        static double angle = 0D;
        public static Bitmap Bake(Camera camera)
        {

            bakeSW.Start();
            Vector2I res = Graphics.RenderResolution;
            int totPixels = res.x * res.y;
            C_CAMERA cam = new C_CAMERA(camera);

            angle += rotationSpeed * Time.DeltaTime;
            rot = Quaternion.CreateFromAxisAngle((Vector3)axis, (float)(angle * 0.0174533D));


            C_VOLUME[] vols = new C_VOLUME[]
            { /*new C_VOLUME(Sphere.Main),*/ 
                new C_VOLUME()
                {
                    rotation = new C_QUATERNION(rot),
                    position = new C_VECTOR3(new Vector3D(-2, 0, 0)), 
                    scale = new C_VECTOR3(new Vector3D(1, 1, 1)), 
                    type = volumeType.box 
                }
            };

            unsafe
            {
                volumeSize = sizeof(C_VOLUME) * vols.Length;
                
            }

            ErrorCode error;

            // Création de la mémoire pour les objets
            memVolume = (Mem)Cl.CreateBuffer(gpu_context, MemFlags.ReadOnly, volumeSize, out error);
            // Met cette mémoire en argument
            Cl.SetKernelArg(kernel, 1, (IntPtr)intPtrSize, memVolume);

            int nVolumeSize = vols.Length * sizeof(int);
            Mem memNVolume = (Mem)Cl.CreateBuffer(gpu_context, MemFlags.ReadOnly, nVolumeSize, out error);

            Cl.SetKernelArg(kernel, 2, (IntPtr)intPtrSize, memNVolume);

            error = Cl.EnqueueWriteBuffer(Queue, (IMem)memInput, Bool.True, IntPtr.Zero, new IntPtr(inputSize), cam, 0, null, out Event event0);
            error = Cl.EnqueueWriteBuffer(Queue, (IMem)memVolume, Bool.True, IntPtr.Zero, new IntPtr(volumeSize), vols, 0, null, out event0);
            error = Cl.EnqueueWriteBuffer(Queue, (IMem)memNVolume, Bool.True, IntPtr.Zero, new IntPtr(nVolumeSize), vols.Length, 0, null, out event0);

            if (error != ErrorCode.Success)
            {
                Log.Print("Error when enqueuing buffer: " + error.ToString());
            }

            byte[] bp = new byte[totPixels * 4];
            sw.Start();
            error = Cl.EnqueueNDRangeKernel(Queue, kernel, 1, null, workGroupSizePtr, null, 0, null, out event0);


            if (error != ErrorCode.Success)
            {
                Log.Print("Error when enqueuing the NDRange of kernel: " + error.ToString());
            }

            Cl.Finish(Queue);

            ErrorCode execError = Cl.EnqueueReadBuffer(Queue, (IMem)memOutput, Bool.True, IntPtr.Zero, memory, bp, 0, null, out event0);
            if (execError != ErrorCode.Success)
            {
                Log.Print("Error while rendering: " + execError.ToString());
                //return new Bitmap(1, 1);
            }
            sw.Stop();
            RenderTime = sw.Elapsed.TotalMilliseconds;
            sw.Reset();

            Bitmap bm = Imaging.RawToImage(bp, res.x, res.y, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            bakeSW.Stop();
            Log.Print("Camera image bake took " + Math.Round(bakeSW.Elapsed.TotalMilliseconds, 2) + "ms");
            bakeSW.Reset();
            return bm;
        }

        private static byte[] PixelToByteArray(PIXEL[] pixels)
        {
            Stopwatch swa = new Stopwatch();
            swa.Start();
            byte[] bPixels = new byte[pixels.Length * 4];
            Parallel.For(0, pixels.Length, i =>
            {
                PIXEL pix = pixels[i];

                //bytes are BGRA format, pixels RGBA
                bPixels[i * 4] = pix.B; //Blue
                bPixels[i * 4 + 1] = pix.G; //Green
                bPixels[i * 4 + 2] = pix.R; //Red
                bPixels[i * 4 + 3] = pix.A; //Alpha
            });
            swa.Stop();
            Log.Print("Converted " + pixels.Length + " pixels to " + bPixels.Length + " bytes in " + Math.Round(swa.Elapsed.TotalMilliseconds, 2) + " ms");
            return bPixels;
        }

       /* private static GraphicalObject[] BuildObjects()
        {
            if (Body.Bodies == null)
            {
                Log.Print("null");
                return new GraphicalObject[1];
            }
            Log.Print("not null");
            Body[] bodies = Body.Bodies.ToArray();
            GraphicalObject[] gos = new GraphicalObject[bodies.Length];

            for (int i = 0; i < bodies.Length; i++)
            {
                GraphicalObject go = new GraphicalObject
                {
                    volume = bodies[i].Volume,
                    material = null
                };
            }

            return gos;
        }*/
    }
}
