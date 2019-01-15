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
    public Stream stream = Stream.Depth;
    Mesh mesh;

    Vector3[] vertices;

    private GCHandle handle;
    private IntPtr verticesPtr;

    readonly AutoResetEvent e = new AutoResetEvent(false);

    PointCloud pc;

    ComputeBuffer vertexBuffer;

    // Use this for initialization
    void Start()
    {
        RsDevice.Instance.OnStart += OnStartStreaming;
        RsDevice.Instance.OnStop += OnStopStreaming;
    }

    private void OnStartStreaming(PipelineProfile activeProfile)
    {
        pc = new PointCloud();

        using (var profile = activeProfile.GetStream(stream))
        {
            if (profile == null)
            {
                Debug.LogWarningFormat("Stream {0} not in active profile", stream);
            }
        }

        using (var profile = activeProfile.GetStream(Stream.Depth) as VideoStreamProfile)
        {
            Assert.IsTrue(SystemInfo.SupportsTextureFormat(TextureFormat.RGFloat));

            vertices = new Vector3[profile.Width * profile.Height];
            handle = GCHandle.Alloc(vertices, GCHandleType.Pinned);
            verticesPtr = handle.AddrOfPinnedObject();

            var indices = new int[(profile.Width - 1) * (profile.Height - 1) * 6];

            var iIdx = 0;
            for (int j = 0; j < profile.Height; j++)
            {
                for (int i = 0; i < profile.Width; i++)
                {
                    if (i < profile.Width - 1 && j < profile.Height - 1)
                    {
                        var idx = i + j * profile.Width;
                        var y = profile.Width;
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

        RsDevice.Instance.onNewSampleSet += OnFrames;
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
        // RealSenseDevice.Instance.onNewSampleSet -= OnFrames;

        e.Reset();

        if (handle.IsAllocated)
            handle.Free();

        if (pc != null)
        {
            pc.Dispose();
            pc = null;
        }
    }

    private void OnFrames(FrameSet frames)
    {
        using (var depthFrame = frames.DepthFrame)
        using (var points = pc.Calculate(depthFrame))
        using (var f = frames.FirstOrDefault<VideoFrame>(stream))
        {
            pc.MapTexture(f);
            memcpy(verticesPtr, points.VertexData, points.Count * 3 * sizeof(float));

            e.Set();
        }
    }

    void Update()
    {
        if (e.WaitOne(0))
            vertexBuffer.SetData(vertices);
    }

    [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    internal static extern IntPtr memcpy(IntPtr dest, IntPtr src, int count);
}
