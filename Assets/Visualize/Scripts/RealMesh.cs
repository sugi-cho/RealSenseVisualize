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

//this class is copy of RealSensePointCloudGenerator class
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RealMesh : RendererBehaviour
{
    public RsFrameProvider source;
    Mesh mesh;

    private GCHandle handle;
    private IntPtr verticesPtr;

    public bool pause;

    public ComputeShader compute;
    Vector3[] vertices;
    ComputeBuffer particleBuffer;
    ComputeBuffer vertexBuffer;
    ComputeBuffer indicesBuffer;

    int numParticles;
    public float particleEmitRate = 0.01f;
    public float impactRadius = 0.25f;
    bool motionParticle;

    FrameQueue q;

    public void SetMotionParticle()
    {
        compute.SetBool("motionEffect", motionParticle = !motionParticle);
    }

    public void ResetParticle()
    {
        var kernel = compute.FindKernel("init");
        compute.SetBuffer(kernel, "_ParticleBuffer", particleBuffer);
        compute.SetBuffer(kernel, "_VertBuffer", vertexBuffer);
        compute.SetBuffer(kernel, "_IndicesBuffer", indicesBuffer);
        compute.SetFloat("numP", 1f / numParticles);
        compute.Dispatch(kernel, numParticles / 8 + 1, 1, 1);
    }

    public void EmitParticle(Vector3 pos)
    {
        pos = transform.InverseTransformPoint(pos);

        var kernel = compute.FindKernel("emitLit");
        compute.SetBuffer(kernel, "_ParticleBuffer", particleBuffer);
        compute.SetBuffer(kernel, "_IndicesBuffer", indicesBuffer);
        compute.SetFloat("numP", 1f / numParticles);
        compute.SetVector("effectPos", pos);
        compute.SetFloat("effectVal", particleEmitRate);
        compute.SetFloat("time", Time.time / 20f);
        compute.Dispatch(kernel, numParticles / 8 + 1, 1, 1);
    }

    public void AddImpact(Vector3 pos)
    {
        pos = transform.InverseTransformPoint(pos);

        var kernel = compute.FindKernel("addImpact");
        compute.SetBuffer(kernel, "_ParticleBuffer", particleBuffer);
        compute.SetFloat("numP", 1f / numParticles);
        compute.SetVector("effectPos", pos);
        compute.SetFloat("effectVal", impactRadius);
        compute.Dispatch(kernel, numParticles / 8 + 1, 1, 1);
    }

    public void HorizonalEffect()
    {
        var kernel = compute.FindKernel("horisonalEffect");
        compute.SetBuffer(kernel, "_ParticleBuffer", particleBuffer);
        compute.Dispatch(kernel, numParticles / 8 + 1, 1, 1);
    }

    public void VerticalEffect()
    {
        var kernel = compute.FindKernel("verticalEffect");
        compute.SetBuffer(kernel, "_ParticleBuffer", particleBuffer);
        compute.Dispatch(kernel, numParticles / 8 + 1, 1, 1);
    }

    public void HeightLimitEffectt(float height)
    {
        var kernel = compute.FindKernel("heightLimitEffect");
        compute.SetBuffer(kernel, "_ParticleBuffer", particleBuffer);
        compute.SetFloat("effectVal", height);
        compute.Dispatch(kernel, numParticles / 8 + 1, 1, 1);
    }

    void Start()
    {
        source.OnStart += OnStartStreaming;
        source.OnStop += OnStopStreaming;
    }

    private void OnStartStreaming(PipelineProfile obj)
    {
        q = new FrameQueue(1);

        using (var depth = obj.GetStream(Stream.Depth) as VideoStreamProfile)
        {
            Assert.IsTrue(SystemInfo.SupportsTextureFormat(TextureFormat.RGFloat));


            numParticles = (depth.Width - 1) * (depth.Height - 1) * 2;

            vertices = new Vector3[depth.Width * depth.Height];
            handle = GCHandle.Alloc(vertices, GCHandleType.Pinned);
            verticesPtr = handle.AddrOfPinnedObject();

            var indices = new int[(depth.Width - 1) * (depth.Height - 1) * 6];

            var iIdx = 0;
            for (int j = 0; j < depth.Height; j++)
            {
                for (int i = 0; i < depth.Width; i++)
                {
                    if (i < depth.Width - 1 && j < depth.Height - 1)
                    {
                        var idx = i + j * depth.Width;
                        var y = depth.Width;
                        indices[iIdx++] = idx + 0;
                        indices[iIdx++] = idx + y;
                        indices[iIdx++] = idx + 1;

                        indices[iIdx++] = idx + 1;
                        indices[iIdx++] = idx + y;
                        indices[iIdx++] = idx + y + 1;
                    }
                }
            }

            particleBuffer = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(VoxelParticle)));
            vertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
            indicesBuffer = new ComputeBuffer(indices.Length, sizeof(int));

            vertexBuffer.SetData(vertices);
            indicesBuffer.SetData(indices);
            renderer.SetBuffer("_VoxelBuffer", particleBuffer);

            SetMotionParticle();
            ResetParticle();

            if (mesh != null)
                Destroy(mesh);

            mesh = new Mesh()
            {
                indexFormat = IndexFormat.UInt32,
            };
            mesh.MarkDynamic();

            mesh.vertices = new Vector3[numParticles];
            var newIdices = Enumerable.Range(0, numParticles).ToArray();

            mesh.SetIndices(newIdices, MeshTopology.Points, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10f);

            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        source.OnNewSample += OnNewSample;
    }

    void OnDestroy()
    {
        OnStopStreaming();
    }

    private void OnApplicationQuit()
    {
        new List<ComputeBuffer>() { particleBuffer, vertexBuffer, indicesBuffer }.ForEach((b) =>
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
        if (pause)
            return;
        if (q != null)
        {
            Frame f;
            if (!q.PollForFrame(out f))
                return;

            using (var points = f as Points)
            {
                var s = points.Count * sizeof(float);
                if (points.VertexData != IntPtr.Zero)
                {
                    memcpy(verticesPtr, points.VertexData, s * 3);
                    vertexBuffer.SetData(vertices);
                }
            }
            var kernel = compute.FindKernel("build");
            compute.SetBuffer(kernel, "_ParticleBuffer", particleBuffer);
            compute.SetBuffer(kernel, "_VertBuffer", vertexBuffer);
            compute.SetBuffer(kernel, "_IndicesBuffer", indicesBuffer);
            compute.SetFloat("dt", Time.deltaTime);
            compute.Dispatch(kernel, numParticles / 8 + 1, 1, 1);
        }
    }

    public struct VoxelParticle
    {
        public Vector3 vert;
        public Vector3 pos;
        public Vector3 vel;
        public Vector3 dir;
        public Vector4 prop;
        public float t;
        public float size;
    }

    [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    internal static extern IntPtr memcpy(IntPtr dest, IntPtr src, int count);
}
