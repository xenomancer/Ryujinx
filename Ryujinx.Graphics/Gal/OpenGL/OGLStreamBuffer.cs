using OpenTK.Graphics.OpenGL;
using System;

namespace Ryujinx.Graphics.Gal.OpenGL
{
    internal class OGLStreamBuffer : IDisposable
    {
        public int Handle { get; protected set; }

        public long Size { get; protected set; }

        protected BufferTarget Target { get; private set; }

        public OGLStreamBuffer(BufferTarget target, long size)
        {
            Target = target;
            Size   = size;

            Handle = GL.GenBuffer();

            GL.BindBuffer(target, Handle);

            GL.BufferData(target, (IntPtr)size, IntPtr.Zero, BufferUsageHint.StreamDraw);
        }

        public void SetData(long size, IntPtr hostAddress)
        {
            GL.BindBuffer(Target, Handle);

            GL.BufferSubData(Target, IntPtr.Zero, (IntPtr)size, hostAddress);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && Handle != 0)
            {
                GL.DeleteBuffer(Handle);

                Handle = 0;
            }
        }
    }
}
