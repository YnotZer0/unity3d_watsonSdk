using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using IBM.Watson.DeveloperCloud.Connection;
using IBM.Watson.DeveloperCloud.Logging;
using IBM.Watson.DeveloperCloud.Utilities;
using IBM.Watson.DeveloperCloud.DataTypes;
using IBM.Watson.DeveloperCloud.Services.TextToSpeech.v1;
using IBM.Watson.DeveloperCloud.Services.SpeechToText.v1;
using IBM.Watson.DeveloperCloud.Services.Assistant.v1;
using CrazyMinnow.SALSA; // Import SALSA from the CrazyMinnow namespace
using FullSerializer;
using UnityEngine.UI;

public class Watson_newServices : MonoBehaviour {
    public string myName;
    public RandomEyes3D randomEyes;
    //    public GameObject[] lookTargets;  //was going to use this for getting the eyes to look at certain directions during a conversation
    public Text ResultsField;

    private int _recordingRoutine = 0;
    private string _microphoneID = null;
    private AudioClip _recording = null;
    private int _recordingBufferSize = 1;
    private int _recordingHZ = 22050;

    //STT_Article service created in Dallas
    //TTS_Article service created in Dallas
    //ASS_Article service created in Frankfurt

    #region PLEASE SET THESE VARIABLES IN THE INSPECTOR
    [Tooltip("The TTS service URL (optional). This defaults to \"https://stream.watsonplatform.net/text-to-speech/api\"")]
    [SerializeField]
    private string _TTSserviceUrl;  //https://stream.watsonplatform.net/text-to-speech/api
    [Tooltip("The TTS authentication username.")]
    [SerializeField]
    private string _TTSusername;
    [Tooltip("The TTS authentication password.")]
    [SerializeField]
    private string _TTSpassword;
    [Tooltip("The IAM TTS apikey.")] 
    [SerializeField]
    private string _TTSiamApikey;  //
    [Tooltip("The IAM TTS url used to authenticate the apikey (optional). This defaults to \"https://iam.bluemix.net/identity/token\".")]
    [SerializeField]
    private string _TTSiamUrl;
    [Tooltip("The STT service URL (optional). This defaults to \"https://stream.watsonplatform.net/speech-to-text/api\"")]
    [SerializeField]
    private string _STTserviceUrl;  //https://stream.watsonplatform.net/speech-to-text/api
    [Tooltip("The STT authentication username.")]
    [SerializeField]
    private string _STTusername;
    [Tooltip("The STT authentication password.")]
    [SerializeField]
    private string _STTpassword;
    [Tooltip("The IAM STT apikey.")]
    [SerializeField]
    private string _STTiamApikey;  //
    [Tooltip("The IAM STT url used to authenticate the apikey (optional). This defaults to \"https://iam.bluemix.net/identity/token\".")]
    [SerializeField]
    private string _STTiamUrl;
    [Tooltip("The ASS service URL (optional). This defaults to \"https://gateway.watsonplatform.net/assistant/api\"")]
    [SerializeField]
    private string _ASSserviceUrl;  //https://gateway-fra.watsonplatform.net/assistant/api
    [Tooltip("The ASS workspaceId to run the example.")]
    [SerializeField]
    private string _ASSworkspaceId;  //
    [Tooltip("The ASS version date with which you would like to use the service in the form YYYY-MM-DD.")]
    [SerializeField]
    private string _ASSversionDate;  //
    [Tooltip("The ASS authentication username.")]
    [SerializeField]
    private string _ASSusername;
    [Tooltip("The ASS authentication password.")]
    [SerializeField]
    private string _ASSpassword;
    [Tooltip("The IAM ASS apikey.")]
    [SerializeField]
    private string _ASSiamApikey;  //
    [Tooltip("The IAM ASS url used to authenticate the apikey (optional). This defaults to \"https://iam.bluemix.net/identity/token\".")]
    [SerializeField]
    private string _ASSiamUrl;
    #endregion

    private TextToSpeech _TTSservice;
    private SpeechToText _STTservice;
    private Assistant _ASSservice;

    private bool _messageTested = false;

    private fsSerializer _serializer = new fsSerializer();
    private Dictionary<string, object> _context = null;
    private bool _waitingForResponse = true;
    private float wait;
    private bool check;

    private AudioClip audioClip; // Link an AudioClip for Salsa3D to play
    private Salsa3D salsa3D;
    private AudioSource audioSrc;

    private string TTS_content = "";
    public bool play = false;



    // Use this for initialization
    void Start () {

        LogSystem.InstallDefaultReactors();
        audioSrc = GetComponent<AudioSource>(); // Get the SALSA AudioSource from this GameObject

        Runnable.Run(CreateTTSService());
        Log.Debug("Start()", "TTS Service connection made successfully");
        Runnable.Run(CreateSTTService());
        Log.Debug("Start()", "STT Service connection made successfully");
        Runnable.Run(CreateASSService());
        Log.Debug("Start()", "ASS Service connection made successfully");

    }


    private IEnumerator CreateTTSService()
    {
        //TEXT TO SPEECH
        //  Create credential and instantiate service
        Credentials TTScredentials = null;
        if (!string.IsNullOrEmpty(_TTSusername) && !string.IsNullOrEmpty(_TTSpassword))
        {
            //  Authenticate using username and password
            TTScredentials = new Credentials(_TTSusername, _TTSpassword, _TTSserviceUrl);
        }
        else if (!string.IsNullOrEmpty(_TTSiamApikey))
        {
            //  Authenticate using iamApikey
            TokenOptions TTStokenOptions = new TokenOptions()
            {
                IamApiKey = _TTSiamApikey,
                IamUrl = _TTSiamUrl
            };

            TTScredentials = new Credentials(TTStokenOptions, _TTSserviceUrl);

            //  Wait for tokendata
            while (!TTScredentials.HasIamTokenData())
                yield return null;
        }
        else
        {
            throw new WatsonException("Please provide either username and password or IAM apikey to authenticate the TTS service.");
        }

        _TTSservice = new TextToSpeech(TTScredentials);
    }

    private IEnumerator CreateSTTService()
    {
        //SPEECH TO TEXT
        //  Create credential and instantiate service
        Credentials STTcredentials = null;
        if (!string.IsNullOrEmpty(_STTusername) && !string.IsNullOrEmpty(_STTpassword))
        {
            //  Authenticate using username and password
            STTcredentials = new Credentials(_STTusername, _STTpassword, _STTserviceUrl);
        }
        else if (!string.IsNullOrEmpty(_STTiamApikey))
        {
            //  Authenticate using iamApikey
            TokenOptions STTtokenOptions = new TokenOptions()
            {
                IamApiKey = _STTiamApikey,
                IamUrl = _STTiamUrl
            };

            STTcredentials = new Credentials(STTtokenOptions, _STTserviceUrl);

            //  Wait for tokendata
            while (!STTcredentials.HasIamTokenData())
                yield return null;
        }
        else
        {
            throw new WatsonException("Please provide either username and password or IAM apikey to authenticate the STT service.");
        }

        _STTservice = new SpeechToText(STTcredentials);
        //_STTservice.StreamMultipart = true;
    }

    private IEnumerator CreateASSService()
    {
        //ASSISTANT
        //  Create credential and instantiate service for ASSISTANT
        Credentials ASScredentials = null;
        if (!string.IsNullOrEmpty(_ASSusername) && !string.IsNullOrEmpty(_ASSpassword))
        {
            //  Authenticate using username and password
            ASScredentials = new Credentials(_ASSusername, _ASSpassword, _ASSserviceUrl);
        }
        else if (!string.IsNullOrEmpty(_ASSiamApikey))
        {
            //  Authenticate using ASSiamApikey
            TokenOptions ASStokenOptions = new TokenOptions()
            {
                IamApiKey = _ASSiamApikey,
                IamUrl = _ASSiamUrl
            };

            ASScredentials = new Credentials(ASStokenOptions, _ASSserviceUrl);

            //  Wait for tokendata
            while (!ASScredentials.HasIamTokenData())
                yield return null;
        }
        else
        {
            throw new WatsonException("Please provide either username and password or IAM apikey to authenticate the ASSISTANT service.");
        }

        _ASSservice = new Assistant(ASScredentials);
        _ASSservice.VersionDate = _ASSversionDate;

        //  Message
        Dictionary<string, object> input = new Dictionary<string, object>();
        //Say HELLO - start the conversation
        input.Add("text", "first hello");
        MessageRequest messageRequest = new MessageRequest()
        {
            Input = input
        };
        _ASSservice.Message(OnCONVMessage, OnCONVFail, _ASSworkspaceId, messageRequest);

    }


    public bool Active
    {
        get { return _STTservice.IsListening; }
        set
        {
            if (value && !_STTservice.IsListening)
            {
                _STTservice.DetectSilence = true;
                _STTservice.EnableWordConfidence = true;
                _STTservice.EnableTimestamps = true;
                _STTservice.SilenceThreshold = 0.01f;
                _STTservice.MaxAlternatives = 0;
                _STTservice.EnableInterimResults = true;
                _STTservice.OnError = OnSTTError;
                _STTservice.InactivityTimeout = -1;
                _STTservice.ProfanityFilter = false;
                _STTservice.SmartFormatting = true;
                _STTservice.SpeakerLabels = false;
                _STTservice.WordAlternativesThreshold = null;
                _STTservice.StartListening(OnSTTRecognize, OnSTTRecognizeSpeaker);
            }
            else if (!value && _STTservice.IsListening)
            {
                _STTservice.StopListening();
            }
        }
    }

    private void StartRecording()
    {
        if (_recordingRoutine == 0)
        {
            UnityObjectUtil.StartDestroyQueue();
            _recordingRoutine = Runnable.Run(RecordingHandler());
        }
    }

    private void StopRecording()
    {
        if (_recordingRoutine != 0)
        {
            Microphone.End(_microphoneID);
            Runnable.Stop(_recordingRoutine);
            _recordingRoutine = 0;
        }
    }

    private void OnSTTError(string error)
    {
        Active = false;
        Log.Debug("STT.OnSTTError()", "Error! {0}", error);
    }

    private IEnumerator RecordingHandler()
    {
        //      Log.Debug("ExampleStreaming.RecordingHandler()", "devices: {0}", Microphone.devices);
        _recording = Microphone.Start(_microphoneID, true, _recordingBufferSize, _recordingHZ);
        yield return null;      // let _recordingRoutine get set..

        if (_recording == null)
        {
            StopRecording();
            yield break;
        }

        bool bFirstBlock = true;
        int midPoint = _recording.samples / 2;
        float[] samples = null;

        while (_recordingRoutine != 0 && _recording != null)
        {
            int writePos = Microphone.GetPosition(_microphoneID);
            if (writePos > _recording.samples || !Microphone.IsRecording(_microphoneID))
            {
                Log.Error("STT.RecordingHandler()", "Microphone disconnected.");

                StopRecording();
                yield break;
            }

            if ((bFirstBlock && writePos >= midPoint)
                || (!bFirstBlock && writePos < midPoint))
            {
                // front block is recorded, make a RecordClip and pass it onto our callback.
                samples = new float[midPoint];
                _recording.GetData(samples, bFirstBlock ? 0 : midPoint);

                AudioData record = new AudioData();
                record.MaxLevel = Mathf.Max(Mathf.Abs(Mathf.Min(samples)), Mathf.Max(samples));
                record.Clip = AudioClip.Create("Recording", midPoint, _recording.channels, _recordingHZ, false);
                record.Clip.SetData(samples, 0);

                _STTservice.OnListen(record);

                bFirstBlock = !bFirstBlock;
            }
            else
            {
                // calculate the number of samples remaining until we ready for a block of audio, 
                // and wait that amount of time it will take to record.
                int remaining = bFirstBlock ? (midPoint - writePos) : (_recording.samples - writePos);
                float timeRemaining = (float)remaining / (float)_recordingHZ;

                yield return new WaitForSeconds(timeRemaining);
            }

        }

        yield break;
    }


    //  private void OnSTTRecognize(SpeechRecognitionEvent result)
    //  updated for Watson SDK 2.4.0 compatability
    private void OnSTTRecognize(SpeechRecognitionEvent result, Dictionary<string, object> customData)
    {
        if (result != null && result.results.Length > 0)
        {
            foreach (var res in result.results)
            {
                foreach (var alt in res.alternatives)
                {
                    string text = string.Format("{0} ({1}, {2:0.00})\n", alt.transcript, res.final ? "Final" : "Interim", alt.confidence);
                    Log.Debug("STT.OnSTTRecognize()", text);
                    ResultsField.text = text;

                    //only send to CONV once we know the user has stopped talking
                    if (res.final)
                    {
                        string _conversationString = alt.transcript;
                        //We can now call the CONV service?
                        Log.Debug("STT.OnSTTRecognize()", _conversationString);

                        Active = false;  //Stop Microphone from listening

                        //  Message
                        Dictionary<string, object> input = new Dictionary<string, object>();

                        input["text"] = _conversationString;
                        MessageRequest messageRequest = new MessageRequest()
                        {
                            Input = input,
                            Context = _context
                        };
                        _ASSservice.Message(OnCONVMessage, OnCONVFail, _ASSworkspaceId, messageRequest);

                    }
                }

                if (res.keywords_result != null && res.keywords_result.keyword != null)
                {
                    foreach (var keyword in res.keywords_result.keyword)
                    {
                        Log.Debug("STT.OnSTTRecognize()", "keyword: {0}, confidence: {1}, start time: {2}, end time: {3}", keyword.normalized_text, keyword.confidence, keyword.start_time, keyword.end_time);
                    }
                }

            }
        }
    }

    //potentially useful to detect difference between different people speaking?
    //  private void OnSTTRecognizeSpeaker(SpeakerRecognitionEvent result)
    //  updated for Watson SDK 2.4.0 compatability
    private void OnSTTRecognizeSpeaker(SpeakerRecognitionEvent result, Dictionary<string, object> customData)
    {
        if (result != null)
        {
            foreach (SpeakerLabelsResult labelResult in result.speaker_labels)
            {
                Log.Debug("ExampleStreaming.OnRecognize()", string.Format("speaker result: {0} | confidence: {3} | from: {1} | to: {2}", labelResult.speaker, labelResult.from, labelResult.to, labelResult.confidence));
            }
        }
    }


    private void OnCONVMessage(object response, Dictionary<string, object> customData)
    {
        Log.Debug("Assistant.OnMessage()", "Response: {0}", customData["json"].ToString());
        //  Convert resp to fsdata
        fsData fsdata = null;
        fsResult r = _serializer.TrySerialize(response.GetType(), response, out fsdata);
        if (!r.Succeeded)
            throw new WatsonException(r.FormattedMessages);

        //  Convert fsdata to MessageResponse
        MessageResponse messageResponse = new MessageResponse();
        object obj = messageResponse;
        r = _serializer.TryDeserialize(fsdata, obj.GetType(), ref obj);
        if (!r.Succeeded)
            throw new WatsonException(r.FormattedMessages);


        //  Set context for next round of messaging
        object _tempContext = null;
        (response as Dictionary<string, object>).TryGetValue("context", out _tempContext);

        if (_tempContext != null)
            _context = _tempContext as Dictionary<string, object>;
        else
            Log.Debug("ExampleConversation.OnMessage()", "Failed to get context");

        //  Get intent
        object tempIntentsObj = null;
        (response as Dictionary<string, object>).TryGetValue("intents", out tempIntentsObj);

        //Need to wrap this in try/catch so don't trigger exception if has no content for some reason
        object _tempText = null;
        (messageResponse.Output as Dictionary<string, object>).TryGetValue("text", out _tempText);
        object _tempTextObj = (_tempText as List<object>)[0];
        string output = _tempTextObj.ToString();

        if (output != null)
        {
            //replace any <waitX> tags with the value expected by the TTS service
            //this was in the "tell me a joke" Watson Conversation, no longer used so not really relevant, but will leave in code
            string replaceActionTags = output.ToString();
            int pos3 = replaceActionTags.IndexOf("<wait3>");
            if (pos3 != -1)
            {
                replaceActionTags = output.Replace("<wait3>", "<break time='3s'/>");
            }
            int pos4 = replaceActionTags.IndexOf("<wait4>");
            if (pos4 != -1)
            {
                replaceActionTags = output.Replace("<wait4>", "<break time='4s'/>");
            }
            int pos5 = replaceActionTags.IndexOf("<wait5>");
            if (pos5 != -1)
            {
                replaceActionTags = output.Replace("<wait5>", "<break time='5s'/>");
            }
            output = replaceActionTags;
        }
        else
        {
            Log.Debug("Extract outputText", "Failed to extract outputText and set for speaking");
        }

        Log.Debug("TTS_content=", output);  //"Hello Good Evening
        TTS_content = output;
        //trigger the Update to PLAY the TTS message
        play = true;
    }


    private void OnCONVFail(RESTConnector.Error error, Dictionary<string, object> customData)
    {
        Log.Error("ExampleConversation.OnFail()", "Error received: {0}", error.ToString());
        _messageTested = false;
    }

    //called by Update() when play=true;
    private void GetTTS()
    {
        //  Synthesize
        Log.Debug("WatsonTTS", "Attempting synthesize.");
        _TTSservice.Voice = VoiceType.en_GB_Kate; // .en_US_Michael; // .en_US_Allison; //.en_GB_Kate;
        _TTSservice.ToSpeech(HandleToSpeechCallback, OnTTSFail, TTS_content, true);
    }

    void HandleToSpeechCallback(AudioClip clip, Dictionary<string, object> customData = null)
    {
        Log.Debug("HandleToSpeechCallback()", "about to play audio from TTS service");
        //If audioSrc is null and you hear no output - are you sure you added this script component to the SALSA_UMA2_DCS GameObject?
        //If you did not, then you will not have the audio linked up correctly.
        if (Application.isPlaying && clip != null && audioSrc != null)
        {
            Log.Debug("HandleToSpeechCallback()", "Has something to say...");
            audioSrc.spatialBlend = 0.0f;
            audioSrc.clip = clip;
            audioSrc.Play();

            //set flag values that can be picked up in the Update() loop
            wait = clip.length;
            check = true;
        }
    }

    private void OnTTSFail(RESTConnector.Error error, Dictionary<string, object> customData)
    {
        Log.Error("WatsonTTS.OnFail()", "Error received: {0}", error.ToString());
    }


    /// <summary>
    /// A coroutine to track a GameObject with a pre-delay and a track duration
    /// </summary>
    /// <param name="preDelay">Pre delay.</param>
    /// <param name="duration">Duration.</param>
    /// <param name="customShapeIndex">Custom shape index.</param>
    /// NOT used - was an idea for getting eyes to look in certain locations during conversation dialog
/*
    IEnumerator Look(float preDelay, float duration, GameObject lookTarget)
    {
        yield return new WaitForSeconds(preDelay);

        Debug.Log("Look=" + "LEFT/RIGHT");
        randomEyes.SetLookTarget(lookTarget);

        yield return new WaitForSeconds(duration);

        randomEyes.SetLookTarget(null);
    }
*/

    // Update is called once per frame
    void Update()
    {

        if (play)
        {
            Debug.Log("play=true in Update");
            play = false;
            Active = false;
            GetTTS();
        }

        if (check)
        {
            //          Debug.Log ("Update() check=true");
            wait -= Time.deltaTime; //reverse count
        }

        if ((wait < 0f) && (check))
        { //check that clip is not playing      
          Debug.Log ("Speech has finished");
            check = false;
            //Now let's start listening again.....
            Active = true;
            StartRecording();
        }
    }

}
