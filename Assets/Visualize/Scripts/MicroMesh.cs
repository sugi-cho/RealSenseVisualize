using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Intel.RealSense;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using System.Runtime.InteropServices;
using System.Threading;

using sugi.cc;

public class MicroMesh : RendererBehaviour
{
    public RsFrameProvider source;
    Mesh mesh;
    Texture2D uvmap;

    Vector3[] vertices;

    private GCHandle handle;
    private IntPtr verticesPtr;

    ComputeBuffer vertexBuffer;
    FrameQueue q;

    // Use this for initialization
    void Start()
    {
        source.OnStart += OnStartStreaming;
        source.OnStop += OnStopStreaming;
    }

    private void OnStartStreaming(PipelineProfile obj)
    {
        q = new FrameQueue(1);

        using (var depth = obj.Streams.FirstOrDefault(s => s.Stream == Stream.Depth) as VideoStreamProfile)
            ResetMesh(depth.Width, depth.Height);

        source.OnNewSample += OnNewSample;
    }

    void ResetMesh(int width, int height)
    {
        Assert.IsTrue(SystemInfo.SupportsTextureFormat(TextureFormat.RGFloat));
        uvmap = new Texture2D(width, height, TextureFormat.RGFloat, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point,
        };

        vertices = new Vector3[width * height];
        handle = GCHandle.Alloc(vertices, GCHandleType.Pinned);
        verticesPtr = handle.AddrOfPinnedObject();

        var indices = new int[(width - 1) * (height - 1) * 6];

        var iIdx = 0;
        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                if (i < width - 1 && j < height - 1)
                {
                    var idx = i + j * width;
                    var y = width;
                    indices[iIdx++] = idx + 0;
                    indices[iIdx++] = idx + 1;
                    indices[iIdx++] = idx + y;

                    indices[iIdx++] = idx + 1;
                    indices[iIdx++] = idx + y + 1;
                    indices[iIdx++] = idx + y;
                }
            }
        }

        vertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);

        vertexBuffer.SetData(vertices);
        renderer.SetBuffer("_Vertex", vertexBuffer);

        if (mesh != null)
            Destroy(mesh);

        mesh = new Mesh()
        {
            indexFormat = IndexFormat.UInt32,
        };
        mesh.MarkDynamic();

        mesh.vertices = vertices;
        mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10f);

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    void OnDestroy()
    {
        OnStopStreaming();
    }

    private void OnApplicationQuit()
    {
        new List<ComputeBuffer>() { vertexBuffer }.ForEach((b) =>
        {
            if (b != null)
                b.Dispose();
            b = null;
        });
    }


    private void OnStopStreaming()
    {
        source.OnNewSample -= OnNewSample;

        if (q != null)
        {
            q.Dispose();
            q = null;
        }

        if (handle.IsAllocated)
            handle.Free();
    }

    void OnNewSample(Frame frame)
    {
        try
        {
            if (frame.IsComposite)
            {
                using (var fs = FrameSet.FromFrame(frame))
                using (var points = TryGetPoints(fs))
                {
                    q.Enqueue(points);
                }
            }
            if (frame is Points)
            {
                q.Enqueue(frame);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }
    private Points TryGetPoints(FrameSet frameset)
    {
        foreach (var f in frameset)
        {
            if (f is Points)
                return f as Points;
            f.Dispose();
        }
        return null;
    }

    void Update()
    {
        if (q != null)
        {
            Frame f;
            if (!q.PollForFrame(out f))
                return;

            using (var points = f as Points)
            {
                var s = points.Count * sizeof(float);
                if (points.TextureData != IntPtr.Zero)
                {
                    uvmap.LoadRawTextureData(points.TextureData, s * 2);
                    uvmap.Apply();
                }
                if (points.VertexData != IntPtr.Zero)
                {
                    memcpy(verticesPtr, points.VertexData, s * 3);
                    vertexBuffer.SetData(vertices);
                }
            }
        }
    }

    [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    internal static extern IntPtr memcpy(IntPtr dest, IntPtr src, int count);
}
