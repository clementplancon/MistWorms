using System;
using System.Reflection;
using UnityEngine;

namespace MistWorms.Assets
{
    internal static class TextureLoader
    {
        // ImageConversion lives in a module compiled against netstandard 2.1; referencing netstandard.dll (+ System.Memory)
        // still fails with CS0518 (ReadOnlySpan overload), so the method is bound once at runtime.
        private static Func<Texture2D, byte[], bool, bool> _loadImage;

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

        /// <summary>PNG bytes to a compressed, mipmapped Texture2D. Normal maps must be loaded linear.</summary>
        public static Texture2D FromPng(byte[] png, string name, bool linear)
        {
            // Size is replaced by LoadImage; the constructor only fixes format, mips and colour space.
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, true, linear);
            if (!LoadImage(tex, png))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.anisoLevel = 4;
            // Compress needs power-of-two-ish sizes (multiple of 4); the contract textures are 512/1024.
            if (tex.width % 4 == 0 && tex.height % 4 == 0) tex.Compress(false);
            tex.Apply(false, true);
            return tex;
        }
    }
}
