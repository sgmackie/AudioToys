using UnityEngine;
using Unity.Collections;
using Unity.Audio;
using Unity.Mathematics;
using Unity.Burst;
using System.Collections;
using System.Collections.Generic;

public enum VCAParameters 
{
    Amplitude
}

public enum VCAProviders 
{
}

[BurstCompile]
public struct VCANode : IAudioKernel<VCAParameters, VCAProviders>
{
    // State
    private const float PI = math.PI;
    private float currentAmplitude;
    private float currentPan;

    // Methods
    public void Initialize() {}

    public void Dispose() {}

    // AudioKernel execution code
    public void Execute(ref ExecuteContext<VCAParameters, VCAProviders> context) 
    {
        // Loop through all the active output contexts
        int outputCount = context.Outputs.Count;
        for(int outIdx = 0; outIdx < outputCount; ++outIdx) 
        {
            // Get the context output buffer & format
            SampleBuffer outputBuffer = context.Outputs.GetSampleBuffer(outIdx);
            int channels    = outputBuffer.Channels;
            int bufferSize  = outputBuffer.Samples;
            int sampleRate  = context.SampleRate;

            // Get the actual sample buffer
            NativeArray<float> buffer = outputBuffer.Buffer;

            // Get the input buffer
            SampleBuffer inputBuffer        = context.Inputs.GetSampleBuffer(outIdx);
            NativeArray<float> sourceBuffer = inputBuffer.Buffer;

            // Fill the buffer
            for(int smpIdx = 0; smpIdx < bufferSize; ++smpIdx) 
            {
                // Get sample from the inlet
                float sample = sourceBuffer[smpIdx];

                // Apply amplitude
                currentAmplitude = math.lerp(currentAmplitude, context.Parameters.GetFloat(VCAParameters.Amplitude, smpIdx), 0.01f); 
                sample *= currentAmplitude;

                // Copy back
                buffer[smpIdx] = sample;
            }
        }    
    }
}