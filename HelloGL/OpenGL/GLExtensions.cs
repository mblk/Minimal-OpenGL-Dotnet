namespace HelloGL.OpenGL;

public static class GLExtensions
{
    public static uint CompileProgram(this GL gl, string vsSource, string fsSource)
    {
        uint vs = gl.CreateShader(GL.ShaderType.VERTEX_SHADER);
        gl.ShaderSource(vs, vsSource);
        gl.CompileShader(vs);
        gl.CheckShader(vs, "Vertex");

        uint fs = gl.CreateShader(GL.ShaderType.FRAGMENT_SHADER);
        gl.ShaderSource(fs, fsSource);
        gl.CompileShader(fs);
        gl.CheckShader(fs, "Fragment");

        uint p = gl.CreateProgram();
        gl.AttachShader(p, vs);
        gl.AttachShader(p, fs);
        gl.LinkProgram(p);
        gl.CheckProgram(p);

        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        return p;
    }

    private static void CheckShader(this GL gl, uint shader, string label)
    {
        int status = gl.GetShaderI(shader, GL.ShaderParameterName.COMPILE_STATUS);
        if (status == 0)
        {
            var infoLog = gl.GetShaderInfoLog(shader);
            throw new Exception($"{label} compile error: {infoLog}");
        }
    }

    private static void CheckProgram(this GL gl, uint prog)
    {
        int status = gl.GetProgramI(prog, GL.ProgramProperty.LINK_STATUS);
        if (status == 0)
        {
            var infoLog = gl.GetProgramInfoLog(prog);
            throw new Exception($"Link error: {infoLog}");
        }
    }
}

