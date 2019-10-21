using UnityEngine;
using Unity.Collections;
using Unity.Audio;
using Unity.Mathematics;
using Unity.Burst;
using System.Collections;
using System.Collections.Generic;

public enum MonoToStereoParameters 
{
    Pan
}

public enum MonoToStereoProviders 
{
}

[BurstCompile]
public struct MonoToStereoNode : IAudioKernel<MonoToStereoParameters, MonoToStereoProviders>
{
    private const float PI = math.PI;
    private float currentPan;

    // Methods
    public void Initialize() 
    {
        currentPan = 0.5f;
    }

    public void Dispose() {}

    // AudioKernel execution code
    public void Execute(ref ExecuteContext<MonoToStereoParameters, MonoToStereoProviders> context) 
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
            // Keep a running count of the sample count * channel count
            int i = 0;
            for(int smpIdx = 0; smpIdx < bufferSize; ++smpIdx) 
            {
                // Get sample from the inlet
                float sample = sourceBuffer[smpIdx];

                // Calculate pan values
                currentPan = math.lerp(currentPan, context.Parameters.GetFloat(MonoToStereoParameters.Pan, smpIdx), 0.01f);
                
                // Pan values range from -100 - 100, so normalise them to linear float (0 - 1)
                float normalisedPan = (currentPan / 200.0f) + 0.5f;
                
                // Calculate pan values - sine law (equal power)
                float leftAmplitude     = math.sin((1.0f - normalisedPan) * (PI / 2.0f)); 
                float rightAmplitude    = math.sin(normalisedPan * (PI / 2.0f));

                // Copy to left and right channels
                buffer[i]               = sample * leftAmplitude;
                buffer[i + 1]           = sample * rightAmplitude;

                // Update the sample index
                i += channels;
            }
        }    
    }
}