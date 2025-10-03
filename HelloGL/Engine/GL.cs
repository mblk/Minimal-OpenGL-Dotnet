﻿using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace HelloGL.Engine;

// TODO: check Cdecl vs Stdcall, maybe not setting is explicitly is the best option

// type mapping:
// GLsizei: int

public sealed unsafe partial class GL
{
    public const int MajorVersion = 4;
    public const int MinorVersion = 3;

    private readonly Func<string, nint> _loader;

    public GL(Func<string, nint> loader)
    {
        _loader = loader;

        LoadFunctions();
        VerifyLoaded();
        PrintInfos();
        EnableDebugOutput();
    }

    private void LoadFunctions()
    {
        _getError = (delegate* unmanaged[Cdecl]<ErrorCode>)Load("glGetError");
        _getString = (delegate* unmanaged[Cdecl]<StringName, IntPtr>)Load("glGetString");
        _getBooleanv = (delegate* unmanaged[Cdecl]<GetPName, byte*, void>)Load("glGetBooleanv");
        _getIntegerv = (delegate* unmanaged[Cdecl]<GetPName, int*, void>)Load("glGetIntegerv");
        _getFloatv = (delegate* unmanaged[Cdecl]<GetPName, float*, void>)Load("glGetFloatv");
        _getDoublev = (delegate* unmanaged[Cdecl]<GetPName, double*, void>)Load("glGetDoublev");

        _enable = (delegate* unmanaged[Cdecl]<EnableCap, void>)Load("glEnable");
        _disable = (delegate* unmanaged[Cdecl]<EnableCap, void>)Load("glDisable");
        _clearColor = (delegate* unmanaged[Cdecl]<float, float, float, float, void>)Load("glClearColor");
        _clear = (delegate* unmanaged[Cdecl]<ClearBufferMask, void>)Load("glClear");
        _polygonMode = (delegate* unmanaged[Cdecl]<TriangleFace, PolygonModeEnum, void>)Load("glPolygonMode");
        _viewport = (delegate* unmanaged[Cdecl]<int, int, int, int, void>)Load("glViewport");

        _genVertexArrays = (delegate* unmanaged[Cdecl]<int, uint*, void>)Load("glGenVertexArrays");
        _bindVertexArray = (delegate* unmanaged[Cdecl]<uint, void>)Load("glBindVertexArray");
        _deleteVertexArrays = (delegate* unmanaged[Cdecl]<int, uint*, void>)Load("glDeleteVertexArrays");

        _genBuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)Load("glGenBuffers");
        _bindBuffer = (delegate* unmanaged[Cdecl]<BufferTarget, uint, void>)Load("glBindBuffer");
        _bufferData = (delegate* unmanaged[Cdecl]<BufferTarget, nint, void*, BufferUsage, void>)Load("glBufferData");
        _bufferSubData = (delegate* unmanaged[Cdecl]<BufferTarget, nint, nint, void*, void>)Load("glBufferSubData");
        _deleteBuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)Load("glDeleteBuffers");

        _enableVertexAttribArray = (delegate* unmanaged[Cdecl]<uint, void>)Load("glEnableVertexAttribArray");
        _disableVertexAttribArray = (delegate* unmanaged[Cdecl]<uint, void>)Load("glDisableVertexAttribArray");
        _vertexAttribPointer = (delegate* unmanaged[Cdecl]<uint, int, VertexAttribPointerType, byte, int, nint, void>)Load("glVertexAttribPointer");
        _vertexAttribIPointer = (delegate* unmanaged[Cdecl]<uint, int, VertexAttribIType, int, nint, void>)Load("glVertexAttribIPointer");
        _vertexAttribLPointer = (delegate* unmanaged[Cdecl]<uint, int, VertexAttribLType, int, nint, void>)Load("glVertexAttribLPointer");
        
        _drawArrays = (delegate* unmanaged[Cdecl]<PrimitiveType, int, uint, void>)Load("glDrawArrays");

        _createShader = (delegate* unmanaged[Cdecl]<ShaderType, uint>)Load("glCreateShader");
        _deleteShader = (delegate* unmanaged[Cdecl]<uint, void>)Load("glDeleteShader");
        _shaderSource = (delegate* unmanaged[Cdecl]<uint, int, byte**, int*, void>)Load("glShaderSource");
        _compileShader = (delegate* unmanaged[Cdecl]<uint, void>)Load("glCompileShader");
        _getShaderiv = (delegate* unmanaged[Cdecl]<uint, ShaderParameterName, int*, void>)Load("glGetShaderiv");
        _getShaderInfoLog = (delegate* unmanaged[Cdecl]<uint, int, int*, byte*, void>)Load("glGetShaderInfoLog");

        _createProgram = (delegate* unmanaged[Cdecl]<uint>)Load("glCreateProgram");
        _deleteProgram = (delegate* unmanaged[Cdecl]<uint, void>)Load("glDeleteProgram");
        _attachShader = (delegate* unmanaged[Cdecl]<uint, uint, void>)Load("glAttachShader");
        _linkProgram = (delegate* unmanaged[Cdecl]<uint, void>)Load("glLinkProgram");
        _getProgramiv = (delegate* unmanaged[Cdecl]<uint, ProgramProperty, int*, void>)Load("glGetProgramiv");
        _getProgramInfoLog = (delegate* unmanaged[Cdecl]<uint, int, int*, byte*, void>)Load("glGetProgramInfoLog");
        _useProgram = (delegate* unmanaged[Cdecl]<uint, void>)Load("glUseProgram");
        _getAttribLocation = (delegate* unmanaged[Cdecl]<uint, sbyte*, int>)Load("glGetAttribLocation");

        _glDebugMessageCallback = (delegate* unmanaged[Cdecl]<nint, nint, void>)Load("glDebugMessageCallback");
        _glDebugMessageControl = (delegate* unmanaged[Cdecl]<DebugSource, DebugType, DebugSeverity, int, uint*, byte, void>)Load("glDebugMessageControl");
        _glObjectLabel = (delegate* unmanaged<ObjectIdentifier, uint, int, sbyte*, void>)Load("glObjectLabel");
        _glObjectPtrLabel = (delegate* unmanaged<void*, int, sbyte*, void>)Load("glObjectPtrLabel");
    }

    private nint Load(string name)
    {
        var p = _loader(name);
        if (p == nint.Zero)
            throw new InvalidOperationException($"GL function not found: {name}");
        return p;
    }

    [Conditional("DEBUG")]
    private void CheckError([CallerMemberName] string? caller = "")
    {
        var hadError = false;
        ErrorCode error;
        while ((error = GetError()) != ErrorCode.NO_ERROR)
        {
            hadError = true;
            Console.WriteLine($"GL error: {caller} -> {error} (0x{(uint)error:X})");
        }

        if (hadError)
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }

            throw new Exception("GL error(s) detected.");
        }
    }

    [Conditional("DEBUG")]
    private void VerifyLoaded() // TODO might not work correctly with AOT
    {
        var type = typeof(GL);
        foreach (var fieldInfo in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            object? value = fieldInfo.GetValue(this);

            if (value is nint addr && addr == 0)
            {
                throw new Exception($"Field {fieldInfo.Name} not initialized");
            }
        }
    }

    [Conditional("DEBUG")]
    private void EnableDebugOutput()
    {
        Enable(EnableCap.DEBUG_OUTPUT);
        Enable(EnableCap.DEBUG_OUTPUT_SYNCHRONOUS);

        DebugMessageCallback(DebugCallback, nint.Zero);

        // Enable all messages
        DebugMessageControl(DebugSource.DONT_CARE, DebugType.DONT_CARE, DebugSeverity.DONT_CARE, 0, null, true);

        // Disable notification severity messages
        //DebugMessageControl(DebugSource.DONT_CARE, DebugType.DONT_CARE, DebugSeverity.NOTIFICATION, 0, null, false);
    }

    private void DebugCallback(DebugSource source, DebugType type, uint id, DebugSeverity severity, int length, sbyte* message, void* userParam)
    {
        string msg = Marshal.PtrToStringAnsi((nint)message, length) ?? string.Empty;

        Console.WriteLine($"GL: {severity}|{source}|{type}: {msg}");
    }

    private void PrintInfos()
    {
        Console.WriteLine("OpenGL initialized:");
        Console.WriteLine($"  Version:    {GetString(StringName.VERSION)}");
        Console.WriteLine($"  Vendor:     {GetString(StringName.VENDOR)}");
        Console.WriteLine($"  Renderer:   {GetString(StringName.RENDERER)}");
        Console.WriteLine($"  SL Version: {GetString(StringName.SHADING_LANGUAGE_VERSION)}");
    }

    #region General functions

    /// <summary>
    /// return error information
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ErrorCode GetError()
    {
        return _getError();
    }
    private delegate* unmanaged[Cdecl]<ErrorCode> _getError;

    /// <summary>
    /// return a string describing the current GL connection
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetString(StringName name)
    {
        nint ptr = _getString(name);
        CheckError();
        return ptr != nint.Zero
            ? Marshal.PtrToStringAnsi(ptr)!
            : string.Empty;
    }
    private delegate* unmanaged[Cdecl]<StringName, nint> _getString;

    /// <summary>
    /// return the value of a selected parameter
    /// </summary>
    /// <param name="pname"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetBoolean(GetPName pname)
    {
        byte b;
        _getBooleanv(pname, &b);
        CheckError();
        return b != 0;
    }
    private delegate* unmanaged[Cdecl]<GetPName, byte*, void> _getBooleanv;

    /// <summary>
    /// return the value of a selected parameter
    /// </summary>
    /// <param name="pname"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInteger(GetPName pname)
    {
        int v;
        _getIntegerv(pname, &v);
        CheckError();
        return v;
    }
    private delegate* unmanaged[Cdecl]<GetPName, int*, void> _getIntegerv;

    /// <summary>
    /// return the value of a selected parameter
    /// </summary>
    /// <param name="pname"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetFloat(GetPName pname)
    {
        float v;
        _getFloatv(pname, &v);
        CheckError();
        return v;
    }
    private delegate* unmanaged[Cdecl]<GetPName, float*, void> _getFloatv;

    /// <summary>
    /// return the value of a selected parameter
    /// </summary>
    /// <param name="pname"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDouble(GetPName pname)
    {
        double v;
        _getDoublev(pname, &v);
        CheckError();
        return v;
    }
    private delegate* unmanaged[Cdecl]<GetPName, double*, void> _getDoublev;

    #endregion

    #region Simple state changes

    /// <summary>
    /// enable server-side GL capabilities
    /// </summary>
    /// <param name="cap"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enable(EnableCap cap)
    {
        _enable(cap);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<EnableCap, void> _enable;

    /// <summary>
    /// disable server-side GL capabilities
    /// </summary>
    /// <param name="cap"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Disable(EnableCap cap)
    {
        _disable(cap);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<EnableCap, void> _disable;

    /// <summary>
    /// select a polygon rasterization mode
    /// </summary>
    /// <param name="face"></param>
    /// <param name="mode"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PolygonMode(TriangleFace face, PolygonModeEnum mode)
    {
        _polygonMode(face, mode);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<TriangleFace, PolygonModeEnum, void> _polygonMode;

    /// <summary>
    /// specify clear values for the color buffers
    /// </summary>
    /// <param name="r"></param>
    /// <param name="g"></param>
    /// <param name="b"></param>
    /// <param name="a"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearColor(float r, float g, float b, float a)
    {
        _clearColor(r, g, b, a);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<float, float, float, float, void> _clearColor;

    /// <summary>
    /// clear buffers to preset values
    /// </summary>
    /// <param name="mask"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(ClearBufferMask mask)
    {
        _clear(mask);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<ClearBufferMask, void> _clear;

    /// <summary>
    /// set the viewport
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Viewport(int x, int y, int width, int height)
    {
        _viewport(x, y, width, height);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<int, int, int, int, void> _viewport;

    #endregion

    #region Vertex array object management

    /// <summary>
    /// generate vertex array object names
    /// </summary>
    /// <param name="n"></param>
    /// <param name="arrays"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GenVertexArrays(int n, uint* arrays)
    {
        _genVertexArrays(n, arrays);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<int, uint*, void> _genVertexArrays;

    /// <summary>
    /// bind a vertex array object
    /// </summary>
    /// <param name="array"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindVertexArray(uint array)
    {
        _bindVertexArray(array);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<uint, void> _bindVertexArray;

    /// <summary>
    /// delete vertex array objects
    /// </summary>
    /// <param name="n"></param>
    /// <param name="arrays"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeleteVertexArrays(int n, uint* arrays)
    {
        _deleteVertexArrays(n, arrays);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<int, uint*, void> _deleteVertexArrays;

    #endregion

    #region Buffer management

    /// <summary>
    /// generate buffer object names
    /// </summary>
    /// <param name="n"></param>
    /// <param name="buffers"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GenBuffers(int n, uint* buffers)
    {
        _genBuffers(n, buffers);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<int, uint*, void> _genBuffers;

    /// <summary>
    /// bind a named buffer object
    /// </summary>
    /// <param name="target"></param>
    /// <param name="buffer"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindBuffer(BufferTarget target, uint buffer)
    {
        _bindBuffer(target, buffer);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<BufferTarget, uint, void> _bindBuffer;

    /// <summary>
    /// delete named buffer objects
    /// </summary>
    /// <param name="n"></param>
    /// <param name="buffers"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeleteBuffers(int n, uint* buffers)
    {
        _deleteBuffers(n, buffers);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<int, uint*, void> _deleteBuffers;


    /// <summary>
    /// creates and initializes a buffer object's data store
    /// </summary>
    /// <param name="target"></param>
    /// <param name="size"></param>
    /// <param name="data"></param>
    /// <param name="usage"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BufferData(BufferTarget target, nint size, void* data, BufferUsage usage)
    {
        _bufferData(target, size, data, usage);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<BufferTarget, nint, void*, BufferUsage, void> _bufferData;

    /// <summary>
    /// updates a subset of a buffer object's data store
    /// </summary>
    /// <param name="target"></param>
    /// <param name="offset"></param>
    /// <param name="size"></param>
    /// <param name="data"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BufferSubData(BufferTarget target, nint offset, nint size, void* data)
    {
        _bufferSubData(target, offset, size, data);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<BufferTarget, nint, nint, void*, void> _bufferSubData;

    #endregion

    #region Vertex attribute management

    /// <summary>
    /// define an array of generic vertex attribute data
    /// </summary>
    /// <param name="index"></param>
    /// <param name="size"></param>
    /// <param name="type"></param>
    /// <param name="normalized"></param>
    /// <param name="stride"></param>
    /// <param name="pointer"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void VertexAttribPointer(uint index, int size, VertexAttribPointerType type, bool normalized, int stride, nint pointer)
    {
        _vertexAttribPointer(index, size, type, BoolToByte(normalized), stride, pointer);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<uint, int, VertexAttribPointerType, byte, int, nint, void> _vertexAttribPointer;

    /// <summary>
    /// define an array of generic vertex attribute data
    /// </summary>
    /// <param name="index"></param>
    /// <param name="size"></param>
    /// <param name="type"></param>
    /// <param name="normalized"></param>
    /// <param name="stride"></param>
    /// <param name="pointer"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void VertexAttribIPointer(uint index, int size, VertexAttribIType type, int stride, nint pointer)
    {
        _vertexAttribIPointer(index, size, type, stride, pointer);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<uint, int, VertexAttribIType, int, nint, void> _vertexAttribIPointer;

    /// <summary>
    /// define an array of generic vertex attribute data
    /// </summary>
    /// <param name="index"></param>
    /// <param name="size"></param>
    /// <param name="type"></param>
    /// <param name="normalized"></param>
    /// <param name="stride"></param>
    /// <param name="pointer"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void VertexAttribLPointer(uint index, int size, VertexAttribLType type, int stride, nint pointer)
    {
        _vertexAttribLPointer(index, size, type, stride, pointer);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<uint, int, VertexAttribLType, int, nint, void> _vertexAttribLPointer;

    /// <summary>
    /// Enable a generic vertex attribute array
    /// </summary>
    /// <param name="index"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnableVertexAttribArray(uint index)
    {
        _enableVertexAttribArray(index);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<uint, void> _enableVertexAttribArray;


    /// <summary>
    /// Disable a generic vertex attribute array
    /// </summary>
    /// <param name="index"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DisableVertexAttribArray(uint index)
    {
        _disableVertexAttribArray(index);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<uint, void> _disableVertexAttribArray;

    #endregion

    #region Drawing commands

    /// <summary>
    /// render primitives from array data
    /// </summary>
    /// <param name="mode"></param>
    /// <param name="first"></param>
    /// <param name="count"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawArrays(PrimitiveType mode, int first, uint count)
    {
        _drawArrays(mode, first, count);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<PrimitiveType, int, uint, void> _drawArrays;

    #endregion

    #region Shader Functions

    /// <summary>
    /// Creates a shader object
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public uint CreateShader(ShaderType type)
    {
        uint shader = _createShader(type);
        CheckError();
        return shader;
    }
    private delegate* unmanaged[Cdecl]<ShaderType, uint> _createShader;

    /// <summary>
    /// Deletes a shader object
    /// </summary>
    /// <param name="shader"></param>
    public void DeleteShader(uint shader)
    {
        _deleteShader(shader);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<uint, void> _deleteShader;

    /// <summary>
    /// Replaces the source code in a shader object
    /// </summary>
    /// <param name="shader"></param>
    /// <param name="source"></param>
    public void ShaderSource(uint shader, string source)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(source);

        fixed (byte* p = bytes)
        {
            byte** strings = stackalloc byte*[1] { p };
            int length = bytes.Length;

            _shaderSource(shader, 1, strings, &length);
        }

        CheckError();
    }
    private delegate* unmanaged[Cdecl]<uint, int, byte**, int*, void> _shaderSource;

    /// <summary>
    /// Compiles a shader object
    /// </summary>
    /// <param name="shader"></param>
    public void CompileShader(uint shader)
    {
        _compileShader(shader);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<uint, void> _compileShader;

    /// <summary>
    /// Returns a parameter from a shader object
    /// </summary>
    /// <param name="shader"></param>
    /// <param name="pname"></param>
    /// <param name="params"></param>
    public void GetShaderIV(uint shader, ShaderParameterName pname, int* @params)
    {
        _getShaderiv(shader, pname, @params);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<uint, ShaderParameterName, int*, void> _getShaderiv;

    /// <summary>
    /// Returns a parameter from a shader object
    /// </summary>
    /// <param name="shader"></param>
    /// <param name="pname"></param>
    /// <returns></returns>
    public int GetShaderI(uint shader, ShaderParameterName pname)
    {
        int value = 0;
        GetShaderIV(shader, pname, &value);
        return value;
    }

    /// <summary>
    /// Returns the information log for a shader object
    /// </summary>
    /// <param name="shader"></param>
    /// <returns></returns>
    public string GetShaderInfoLog(uint shader)
    {
        int len = GetShaderI(shader, ShaderParameterName.INFO_LOG_LENGTH);
        if (len < 1)
            return String.Empty;

        Span<byte> buffer = len < 256
            ? stackalloc byte[len]
            : new byte[len];

        fixed (byte* p = buffer)
        {
            _getShaderInfoLog(shader, len, null, p);
        }
        CheckError();

        var infoLog = Encoding.UTF8.GetString(buffer).TrimEnd('\0', '\n', '\r');
        return infoLog;
    }
    private delegate* unmanaged[Cdecl]<uint, int, int*, byte*, void> _getShaderInfoLog;

    #endregion

    #region Program Functions

    /// <summary>
    /// Creates a program object
    /// </summary>
    /// <returns></returns>
    public uint CreateProgram()
    {
        uint program = _createProgram();
        CheckError();
        return program;
    }
    private delegate* unmanaged[Cdecl]<uint> _createProgram;

    /// <summary>
    /// Deletes a program object
    /// </summary>
    /// <param name="program"></param>
    public void DeleteProgram(uint program)
    {
        _deleteProgram(program);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<uint, void> _deleteProgram;

    /// <summary>
    /// Attaches a shader object to a program object
    /// </summary>
    /// <param name="program"></param>
    /// <param name="shader"></param>
    public void AttachShader(uint program, uint shader)
    {
        _attachShader(program, shader);
        CheckError();

    }
    private delegate* unmanaged[Cdecl]<uint, uint, void> _attachShader;

    /// <summary>
    /// Links a program object
    /// </summary>
    /// <param name="program"></param>
    public void LinkProgram(uint program)
    {
        _linkProgram(program);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<uint, void> _linkProgram;

    /// <summary>
    /// Returns a parameter from a program object
    /// </summary>
    /// <param name="program"></param>
    /// <param name="prop"></param>
    /// <param name="params"></param>
    public void GetProgramIV(uint program, ProgramProperty prop, int* @params)
    {
        _getProgramiv(program, prop, @params);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<uint, ProgramProperty, int*, void> _getProgramiv;

    /// <summary>
    /// Returns a parameter from a program object
    /// </summary>
    /// <param name="program"></param>
    /// <param name="prop"></param>
    /// <returns></returns>
    public int GetProgramI(uint program, ProgramProperty prop)
    {
        int value = 0;
        GetProgramIV(program, prop, &value);
        return value;
    }

    /// <summary>
    /// Returns the information log for a program object
    /// </summary>
    /// <param name="program"></param>
    /// <returns></returns>
    public string GetProgramInfoLog(uint program)
    {
        int len = GetProgramI(program, ProgramProperty.INFO_LOG_LENGTH);
        if (len < 1)
            return String.Empty;

        Span<byte> buffer = len < 256
            ? stackalloc byte[len]
            : new byte[len];

        fixed (byte* p = buffer)
        {
            _getProgramInfoLog(program, len, null, p);
        }
        CheckError();

        var infoLog = Encoding.UTF8.GetString(buffer).TrimEnd('\0', '\n', '\r');
        return infoLog;
    }
    private delegate* unmanaged[Cdecl]<uint, int, int*, byte*, void> _getProgramInfoLog;

    /// <summary>
    /// Installs a program object as part of current rendering state
    /// </summary>
    /// <param name="program"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UseProgram(uint program)
    {
        _useProgram(program);
        CheckError();
    }
    private delegate* unmanaged[Cdecl]<uint, void> _useProgram;

    /// <summary>
    /// Returns the location of an attribute variable
    /// </summary>
    /// <param name="program"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public int GetAttribLocation(uint program, scoped ReadOnlySpan<byte> name)
    {
#if DEBUG
        if (name.Length < 1) throw new ArgumentException("Name must not be empty.", nameof(name));
#endif

        ReadOnlySpan<byte> terminatedName = name;

        if (name[^1] != 0)
        {
            Span<byte> buffer = name.Length < 256
                ? stackalloc byte[name.Length + 1]
                : new byte[name.Length + 1];

            name.CopyTo(buffer);
            buffer[^1] = 0;

            terminatedName = buffer;
        }

        fixed (byte* p = terminatedName)
        {
            int loc = _getAttribLocation(program, (sbyte*)p);
            CheckError();
            return loc;
        }
    }
    private unsafe delegate* unmanaged[Cdecl]<uint, sbyte*, int> _getAttribLocation;

    /// <summary>
    /// Returns the location of an attribute variable
    /// </summary>
    /// <param name="program"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public int GetAttribLocation(uint program, string name)
    {
#if DEBUG
        if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name must not be empty.", nameof(name));
#endif

        int len = Encoding.UTF8.GetByteCount(name);

        Span<byte> buffer = len < 256
            ? stackalloc byte[len + 1]
            : new byte[len + 1];

        Encoding.UTF8.GetBytes(name, buffer[..len]);
        buffer[^1] = 0;

        return GetAttribLocation(program, buffer);
    }

    #endregion

    #region Debugging

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void DebugProc(DebugSource source, DebugType type, uint id, DebugSeverity severity, int length, sbyte* message, void* userParam);

    /// <summary>
    /// specify a callback to receive debugging messages from the GL
    /// </summary>
    /// <param name="callback"></param>
    /// <param name="userParam"></param>
    public void DebugMessageCallback(DebugProc callback, nint userParam)
    {
        _debugProcs.Add(callback); // Prevent GC
        nint addr = Marshal.GetFunctionPointerForDelegate(callback);

        _glDebugMessageCallback(addr, userParam);
        CheckError();
    }
    private unsafe delegate* unmanaged[Cdecl]<nint, nint, void> _glDebugMessageCallback;
    private readonly List<DebugProc> _debugProcs = [];

    /// <summary>
    /// control the reporting of debug messages in a debug context
    /// </summary>
    /// <param name="source"></param>
    /// <param name="type"></param>
    /// <param name="severity"></param>
    /// <param name="count"></param>
    /// <param name="ids"></param>
    /// <param name="enabled"></param>
    public void DebugMessageControl(DebugSource source, DebugType type, DebugSeverity severity, int count, uint* ids, bool enabled)
    {
        _glDebugMessageControl(source, type, severity, count, ids, BoolToByte(enabled));
        CheckError();
    }
    private unsafe delegate* unmanaged[Cdecl]<DebugSource, DebugType, DebugSeverity, int, uint*, byte, void> _glDebugMessageControl;

    /// <summary>
    /// label a named object identified within a namespace
    /// </summary>
    /// <param name="identifier"></param>
    /// <param name="name"></param>
    /// <param name="label"></param>
    public void ObjectLabel(ObjectIdentifier identifier, uint name, string label)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(label);
        fixed (byte* p = bytes)
        {
            _glObjectLabel(identifier, name, bytes.Length, (sbyte*)p);
        }
        CheckError();
    }
    private unsafe delegate* unmanaged<ObjectIdentifier, uint, int, sbyte*, void> _glObjectLabel;

    /// <summary>
    /// label a sync object identified by a pointer
    /// </summary>
    /// <param name="pointer"></param>
    /// <param name="label"></param>
    public void ObjectPtrLabel(void* pointer, string label)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(label);
        fixed (byte* p = bytes)
        {
            _glObjectPtrLabel(pointer, bytes.Length, (sbyte*)p);
        }
        CheckError();
    }
    private unsafe delegate* unmanaged<void*, int, sbyte*, void> _glObjectPtrLabel;

    #endregion

    #region Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte BoolToByte(bool v) => v ? (byte)1 : (byte)0;

    #endregion
}
