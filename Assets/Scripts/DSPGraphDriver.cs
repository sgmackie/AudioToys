using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Audio;

[BurstCompile]
public struct DSPGraphDriver : IAudioOutput 
{
    public DSPGraph     m_graph;
    private int         m_channels;
    private SoundFormat m_format;
    private int         m_sampleRate;
    private long        m_bufferSize;

    public void Initialize(int inputChannelCount, SoundFormat inputFormat, int inputSampleRate, long inputBufferSize) 
    {
        m_channels      = inputChannelCount;
        m_format        = inputFormat;
        m_sampleRate    = inputSampleRate;
        m_bufferSize    = inputBufferSize;

        switch(inputFormat)
        {
            case SoundFormat.Stereo:
            {
                m_graph = DSPGraph.Create(m_format, m_channels, (int) m_bufferSize, m_sampleRate);
                break;
            }
        }
    }

    public void BeginMix(int frameCount) 
    {
        m_graph.BeginMix(frameCount);
    }

    public void EndMix(NativeArray<float> outputBuffer, int frameCount) 
    {
        m_graph.ReadMix(outputBuffer, frameCount, m_channels);
    }

    public void Dispose() 
    {
        m_graph.Dispose();
    }
}