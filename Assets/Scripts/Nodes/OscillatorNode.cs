using UnityEngine;
using Unity.Collections;
using Unity.Audio;
using Unity.Mathematics;
using Unity.Burst;
using System.Collections;
using System.Collections.Generic;

public enum OscParameters 
{
    Wave,
    Frequency
}

public enum OscProviders 
{
}

public enum OscWave
{
    Sine,
    Square
}

[BurstCompile]
public struct OscillatorNode : IAudioKernel<OscParameters, OscProviders>
{
    // Convience const
    private const float PI = math.PI;
    private const float TWO_PI = (math.PI * 2.0f);

    // State
    private float currentPhase;
    private float phaseIncrement;
    private float currentFrequency;
    private OscWave currentWave;

    // Methods
    public void Initialize() {}

    public void Dispose() {}

    // AudioKernel execution code
    public void Execute(ref ExecuteContext<OscParameters, OscProviders> context) 
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

            // Switch on the wave type
            currentWave = (OscWave) context.Parameters.GetFloat(OscParameters.Wave, 0);
            switch(currentWave)
            {
                case OscWave.Sine:
                {
                    Sine(ref context.Parameters, ref buffer, bufferSize, sampleRate);
                    break;
                }
                case OscWave.Square:
                {
                    Square(ref context.Parameters, ref buffer, bufferSize, sampleRate);
                    break;
                }
                default:
                {
                    Sine(ref context.Parameters, ref buffer, bufferSize, sampleRate);
                    break;
                }
            }
        }
    }

    private void Sine(ref ParameterData<OscParameters> parameters, ref NativeArray<float> buffer, int bufferSize, int sampleRate)
    {
        // Fill the buffer
        for(int smpIdx = 0; smpIdx < bufferSize; ++smpIdx) 
        {
            // Set internal state through interpolation over the sample count
            currentFrequency = math.lerp(currentFrequency, parameters.GetFloat(OscParameters.Frequency, smpIdx), 0.01f);

            // Update the increment - 2pi over the sample rate
            phaseIncrement = ((TWO_PI * currentFrequency) / (float) sampleRate);

            // Generate sample
            float sample = math.sin(currentPhase);
            buffer[smpIdx] = sample;
            
            // Increment phasor
            currentPhase += phaseIncrement;

            // Wrap the phasor
            PhaseWrap(ref currentPhase, TWO_PI);   
        }
    }

    private void Square(ref ParameterData<OscParameters> parameters, ref NativeArray<float> buffer, int bufferSize, int sampleRate)
    {
        // Fill the buffer
        for(int smpIdx = 0; smpIdx < bufferSize; ++smpIdx) 
        {
            // Set internal state through interpolation over the sample count
            currentFrequency = math.lerp(currentFrequency, parameters.GetFloat(OscParameters.Frequency, smpIdx), 0.01f);

            // Update the increment - 2pi over the sample rate
            phaseIncrement = ((TWO_PI * currentFrequency) / (float) sampleRate);

            // Generate sample
            float sample = 0.0f;
            if(currentPhase <= PI)
            {
                sample = 1.0f;
            }
            else
            {
                sample = -1.0f;
            }
            buffer[smpIdx] = sample;

            // Increment phasor
            currentPhase += phaseIncrement;

            // Wrap the phasor
            PhaseWrap(ref currentPhase, TWO_PI);     
        }
    }

    private void PhaseWrap(ref float phasor, float limit)
    {
        while(phasor >= limit)
        {
            phasor -= limit;
        }
        while(phasor < 0)
        {
            phasor += limit;
        } 

    } 
}