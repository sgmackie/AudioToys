using UnityEngine;
using Unity.Collections;
using Unity.Audio;
using Unity.Mathematics;
using Unity.Burst;
using System.Collections;
using System.Collections.Generic;

public enum SVFParameters 
{
    Cutoff,
    Q,
    Amplitude
}

public enum SVFProviders 
{
}

// TODO: Notate in an MD file the Vadim digital model for this (check Pirkle book again)
[BurstCompile]
public struct SVFNode : IAudioKernel<SVFParameters, SVFProviders>
{
    // State
    private const double        TWO_PI = (math.PI * 2.0);
    private double              alpha0;
    private double              alpha;
    private double              rho;
    private double              analogMatchSigma;
    private NativeArray<double> delay;

    // Parameter state
    private double              lastQ;
    private double              lastCutoff;
    private float               currentAmplitude;

    // Methods
    public void Initialize()
    {
        int order = 2 + 1;
        delay = new NativeArray<double>(order, Allocator.AudioKernel, NativeArrayOptions.ClearMemory);
    }

    public void Dispose() 
    {
        delay.Dispose();
    }

    // AudioKernel execution code
    public void Execute(ref ExecuteContext<SVFParameters, SVFProviders> context) 
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

            // Check if coefficients need to be updated
            double currentQ = context.Parameters.GetFloat(SVFParameters.Q, 0);
            double currentCutoff = context.Parameters.GetFloat(SVFParameters.Cutoff, 0);
            if(lastQ != currentQ || lastCutoff != currentCutoff)
            {
                CalculateCoefficients(ref context.Parameters, currentQ, currentCutoff, sampleRate);
            }

            // Fill the buffer
            for(int smpIdx = 0; smpIdx < bufferSize; ++smpIdx) 
            {
                // Get sample from the inlet
                float sample = sourceBuffer[smpIdx];

                // Calculate bands
                double hpf = alpha0 * (sample - rho * delay[0] - delay[1]);
                double bpf = alpha * hpf + delay[0];
                double lpf = alpha * bpf + delay[1];
                double bsf = hpf + lpf;
                
                // Cache
                double sn = delay[0];

                // update memory
                delay[0] = alpha * hpf + bpf;
                delay[1] = alpha * bpf + lpf;

                // Apply gain
                currentAmplitude = math.lerp(currentAmplitude, context.Parameters.GetFloat(SVFParameters.Amplitude, smpIdx), 0.01f); 
                sample = ((float) (currentAmplitude * lpf));

                // Copy back
                buffer[smpIdx] = sample;
            }

            // Cache parameters
            lastQ        = currentQ;
            lastCutoff   = currentCutoff;
        }    
    }

    private void CalculateCoefficients(ref ParameterData<SVFParameters> parameters, double q, double fc, int sampleRate)
    {
        double wd = TWO_PI * fc;
        double T = 1.0 / (double) sampleRate;
        double wa = (2.0 / T) * math.tan(wd * T / 2.0);
        double g = wa * T / 2.0;

        double R = 0.0;
        alpha0 = 1.0 / (1.0 + 2.0 * R * g + g * g);
        alpha = g;
        rho = 2.0 * R + g;
        double f_o = (sampleRate / 2.0) / fc;
        analogMatchSigma = 1.0 / (alpha* f_o * f_o);
    }

}