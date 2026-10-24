using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace MistWorms.Assets
{
    internal static class TextureLoader
    {
        // ImageConversion lives in a module compiled against netstandard 2.1; referencing netstandard.dll (+ System.Memory)
        // still fails with CS0518 (ReadOnlySpan overload), so the method is bound once at runtime.
        private static Func<Texture2D, byte[], bool, bool> _loadImage;

        /// <summary>Cumulative Stopwatch ticks per stage since <see cref="ResetTimings"/> (decode, compress, apply + mipmaps).</summary>
        public static long DecodeTicks, CompressTicks, ApplyTicks;

        public static void ResetTimings() { DecodeTicks = CompressTicks = ApplyTicks = 0; }

        public static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        private static bool LoadImage(Texture2D tex, byte[] png)
        {
            if (_loadImage == null)
            {
                Type t = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", true);
                MethodInfo m = t.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
                _loadImage = (Func<Texture2D, byte[], bool, bool>)Delegate.CreateDelegate(typeof(Func<Texture2D, byte[], bool, bool>), m);
            }
            return _loadImage(tex, png, false);
        }

        /// <summary>
        /// PNG bytes to a compressed, mipmapped Texture2D. Normal and lustre maps must be loaded linear. The alpha channel
        /// of an RGBA PNG is kept (DXT5). <paramref name="point"/> selects FilterMode.Point, the filter of the vanilla creature textures.
        /// </summary>
        public static Texture2D FromPng(byte[] png, string name, bool linear, bool point = false)
        {
            // Size is replaced by LoadImage; the constructor only fixes format, mips and colour space.
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, true, linear);
            long t0 = Stopwatch.GetTimestamp();
            bool decoded = LoadImage(tex, png);
            DecodeTicks += Stopwatch.GetTimestamp() - t0;
            if (!decoded)
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
            tex.anisoLevel = point ? 1 : 4;
            // Compress needs power-of-two-ish sizes (multiple of 4); the contract textures are 128 (worm) and 1024.
            t0 = Stopwatch.GetTimestamp();
            if (tex.width % 4 == 0 && tex.height % 4 == 0) tex.Compress(false);
            long t1 = Stopwatch.GetTimestamp();
            CompressTicks += t1 - t0;
            tex.Apply(false, true);
            ApplyTicks += Stopwatch.GetTimestamp() - t1;
            return tex;
        }
    }
}
