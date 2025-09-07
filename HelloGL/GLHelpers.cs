using System.Text;

internal static class GLHelpers
{
    public static unsafe uint CompileProgram(GL gl, string vsSource, string fsSource)
    {
        uint vs = gl.CreateShader(GL.GL_VERTEX_SHADER);
        GLSetSource(gl, vs, vsSource);
        gl.CompileShader(vs);
        CheckShader(gl, vs, "Vertex");

        uint fs = gl.CreateShader(GL.GL_FRAGMENT_SHADER);
        GLSetSource(gl, fs, fsSource);
        gl.CompileShader(fs);
        CheckShader(gl, fs, "Fragment");

        uint p = gl.CreateProgram();
        gl.AttachShader(p, vs);
        gl.AttachShader(p, fs);
        gl.LinkProgram(p);
        CheckProgram(gl, p);

        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        return p;
    }

    private static unsafe void GLSetSource(GL gl, uint shader, string src)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(src);
        fixed (byte* p = bytes)
        {
            byte*[] arr = new byte*[1] { p };
            int len = bytes.Length;
            fixed (byte** parr = arr)
            {
                gl.ShaderSource(shader, 1, parr, &len);
            }
        }
    }

    private static unsafe void CheckShader(GL gl, uint shader, string label)
    {
        int status = 0;
        gl.GetShaderiv(shader, GL.GL_COMPILE_STATUS, &status);
        if (status == 0)
        {
            int len = 0;
            gl.GetShaderiv(shader, GL.GL_INFO_LOG_LENGTH, &len);
            var sb = new byte[Math.Max(len, 1024)];
            fixed (byte* p = sb)
            {
                gl.GetShaderInfoLog(shader, sb.Length, IntPtr.Zero, (IntPtr)p);
                throw new Exception($"{label} compile error: {Encoding.ASCII.GetString(sb)}");
            }
        }
    }

    private static unsafe void CheckProgram(GL gl, uint prog)
    {
        int status = 0;
        gl.GetProgramiv(prog, GL.GL_LINK_STATUS, &status);
        if (status == 0)
        {
            int len = 0;
            gl.GetProgramiv(prog, GL.GL_INFO_LOG_LENGTH, &len);
            var sb = new byte[Math.Max(len, 1024)];
            fixed (byte* p = sb)
            {
                gl.GetProgramInfoLog(prog, sb.Length, IntPtr.Zero, (IntPtr)p);
                throw new Exception($"Link error: {Encoding.ASCII.GetString(sb)}");
            }
        }
    }
}

