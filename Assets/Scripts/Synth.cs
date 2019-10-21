using UnityEngine;
using Unity.Collections;
using Unity.Audio;
using Unity.Mathematics;
using Unity.Burst;
using System.Collections;
using System.Collections.Generic;

// TODO: Polyphony
// TODO: Dynamic node creation
// TODO: ADSR node
public class Synth : MonoBehaviour 
{
    // Output format
    public int                  m_sampleRate = 48000;
    public int                  m_bufferSize = 1024;
    public int                  m_channels   = 2;

    // Graph and output
    private DSPGraphDriver      m_driver;
    private DSPNode             m_oscNode;
    private DSPNode             m_filterNode;
    private DSPNode             m_vcaNode;
    private DSPNode             m_stereoNode;
    private NativeArray<float>  m_buffer;

    // Internal state
    private bool                m_IsRunning;

    // Script load constructor
    private void Awake() 
    {
        AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
        m_sampleRate = audioConfig.sampleRate;
        m_bufferSize = audioConfig.dspBufferSize;

        switch(audioConfig.speakerMode)
        {
            case AudioSpeakerMode.Mono:
            {
                m_channels = 1;
                break;
            }
            case AudioSpeakerMode.Stereo:
            {
                m_channels = 2;
                break;
            }
            default:
            {
                m_channels = 2;
                break;
            }
        }

        // Create output
        m_driver = new DSPGraphDriver();
        m_driver.Initialize(m_channels, SoundFormat.Stereo, m_sampleRate, m_bufferSize);
        int bufferAllocationSize = (m_channels * m_bufferSize);
        m_buffer = new NativeArray<float>(bufferAllocationSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        // Connect internal node to the output
        DSPCommandBlock commandBlock = m_driver.m_graph.CreateCommandBlock();
        m_oscNode = commandBlock.CreateDSPNode<OscParameters, OscProviders, OscillatorNode>();
        commandBlock.AddOutletPort(m_oscNode, 1, SoundFormat.Mono);
        commandBlock.SetFloat<OscParameters, OscProviders, OscillatorNode>(m_oscNode, OscParameters.Wave, (float) OscWave.Sine);
        commandBlock.SetFloat<OscParameters, OscProviders, OscillatorNode>(m_oscNode, OscParameters.Frequency, 440.0f);

        // SVF (2nd order low pass) node
        m_filterNode = commandBlock.CreateDSPNode<SVFParameters, SVFProviders, SVFNode>();
        commandBlock.AddInletPort(m_filterNode, 1, SoundFormat.Mono);
        commandBlock.AddOutletPort(m_filterNode, 1, SoundFormat.Mono);        
        commandBlock.SetFloat<SVFParameters, SVFProviders, SVFNode>(m_filterNode, SVFParameters.Cutoff, 100.0f); 
        commandBlock.SetFloat<SVFParameters, SVFProviders, SVFNode>(m_filterNode, SVFParameters.Q, 0.707f); 
        commandBlock.SetFloat<SVFParameters, SVFProviders, SVFNode>(m_filterNode, SVFParameters.Amplitude, 0.8f); 

        // VCA (amplitude control) node
        m_vcaNode = commandBlock.CreateDSPNode<VCAParameters, VCAProviders, VCANode>();
        commandBlock.AddInletPort(m_vcaNode, 1, SoundFormat.Mono);
        commandBlock.AddOutletPort(m_vcaNode, 1, SoundFormat.Mono);        
        commandBlock.SetFloat<VCAParameters, VCAProviders, VCANode>(m_vcaNode, VCAParameters.Amplitude, 0.0f);

        // Mono to stereo node setup
        m_stereoNode = commandBlock.CreateDSPNode<MonoToStereoParameters, MonoToStereoProviders, MonoToStereoNode>();
        commandBlock.AddInletPort(m_stereoNode, 1, SoundFormat.Mono);
        commandBlock.AddOutletPort(m_stereoNode, m_channels, SoundFormat.Stereo);
        commandBlock.SetFloat<MonoToStereoParameters, MonoToStereoProviders, MonoToStereoNode>(m_stereoNode, MonoToStereoParameters.Pan, 0.0f);

        // Connect the nodes together
        // VCO -> SVF -> VCA -> Pan -> Output
        commandBlock.Connect(m_oscNode, 0, m_filterNode, 0);
        commandBlock.Connect(m_filterNode, 0, m_vcaNode, 0);
        commandBlock.Connect(m_vcaNode, 0, m_stereoNode, 0);
        commandBlock.Connect(m_stereoNode, 0, m_driver.m_graph.RootDSP, 0);

        // Send the command the atomic/asynchronous handler
        commandBlock.Complete();
    }

    private IEnumerator Start() 
    {   
        // Wait for NativeArray to kick in
        yield return new WaitForSeconds(0.5f);

        // Start mix
        m_driver.BeginMix(m_bufferSize);
        m_IsRunning = true;
    }

    private void OnDestroy() 
    {
        // Disconnect all the node input/outputs
        DSPCommandBlock commandBlock = m_driver.m_graph.CreateCommandBlock();
        commandBlock.ReleaseDSPNode(m_stereoNode);
        commandBlock.ReleaseDSPNode(m_vcaNode);
        commandBlock.ReleaseDSPNode(m_filterNode);
        commandBlock.ReleaseDSPNode(m_oscNode);
        commandBlock.Complete();

        // Deallocate
        m_driver.Dispose();
        m_buffer.Dispose();
    }

    private void Update() 
    {
        InputUpdate();
    }

    public double Map(double x, double in_min, double in_max, double out_min, double out_max, bool clamp = false)
    {
        if (clamp) x = math.max(in_min, math.min(x, in_max));
        return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
    }

    void InputUpdate() 
    {
        float frequency = 440.0f;
        float amplitude = 0.0f; 
        float cutoff = 100.0f; 
        float q = 0.707f;
        float filterAmplitude = 0.1f;
        float pan = 0; 

        if(Input.GetKey(KeyCode.Mouse0))
        {
            frequency = 440.0f + Input.mousePosition.y;
            pan = (float) Map(Input.mousePosition.x, Screen.width, Screen.height, -100, 100);
            cutoff = 1000.0f - Input.mousePosition.x;
            amplitude = Input.GetKey(KeyCode.Mouse0) ? 0.5f : 0.0f;
            filterAmplitude = (float) Map(Input.mousePosition.y, Screen.width, Screen.height, 0.0, 0.5);
            SendCommand(OscWave.Sine, frequency, amplitude, cutoff, q, filterAmplitude, pan);
        }

        else if(Input.GetKey(KeyCode.Space))
        {
            frequency = 440.0f + Input.mousePosition.y;
            cutoff = 1000.0f - Input.mousePosition.x;
            amplitude = Input.GetKey(KeyCode.Space) ? 0.5f : 0.0f;
            SendCommand(OscWave.Square, frequency, amplitude, cutoff, q, filterAmplitude, pan);
        }

        if(Input.GetKeyUp(KeyCode.Mouse0) || Input.GetKeyUp(KeyCode.Space)) 
        {
            SendCommand(OscWave.Sine, frequency, amplitude, cutoff, q, filterAmplitude, pan);
        }
    }

    void SendCommand(OscWave wave, float frequency, float amplitude, float cutoff, float q, float filterAmplitude, float pan) 
    {
        DSPCommandBlock commandBlock = m_driver.m_graph.CreateCommandBlock();
        commandBlock.SetFloat<OscParameters, OscProviders, OscillatorNode>(m_oscNode, OscParameters.Wave, (float) wave);
        commandBlock.SetFloat<OscParameters, OscProviders, OscillatorNode>(m_oscNode, OscParameters.Frequency, frequency);
        commandBlock.SetFloat<SVFParameters, SVFProviders, SVFNode>(m_filterNode, SVFParameters.Cutoff, cutoff);
        commandBlock.SetFloat<SVFParameters, SVFProviders, SVFNode>(m_filterNode, SVFParameters.Q, q);
        commandBlock.SetFloat<SVFParameters, SVFProviders, SVFNode>(m_filterNode, SVFParameters.Amplitude, filterAmplitude); 
        commandBlock.SetFloat<VCAParameters, VCAProviders, VCANode>(m_vcaNode, VCAParameters.Amplitude, amplitude);
        commandBlock.SetFloat<MonoToStereoParameters, MonoToStereoProviders, MonoToStereoNode>(m_stereoNode, MonoToStereoParameters.Pan, pan);
        commandBlock.Complete();
    }

    // Pass internal buffer to the output
    // TODO: Work out how to use AudioOutputHandle + AttachToDefaultDevice
    private void OnAudioFilterRead(float[] outputBuffer, int outputChannels) 
    {
        if(!m_IsRunning) 
        {
            return;
        }
        
        // Get samples from the graph and pass to the AudioSource
        m_driver.EndMix(m_buffer, m_bufferSize);

        // Copy
        int dataLength = outputBuffer.Length;
        for(int i = 0; i < dataLength; ++i) 
        {
            outputBuffer[i] = m_buffer[i];
        }

        // Reset buffer
        m_driver.BeginMix(m_bufferSize);
    }
}
