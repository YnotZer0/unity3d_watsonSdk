
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IBM.Watson.DeveloperCloud.Services.TextToSpeech.v1;
using IBM.Watson.DeveloperCloud.Logging;
using IBM.Watson.DeveloperCloud.Utilities;
using IBM.Watson.DeveloperCloud.Connection;
using CrazyMinnow.SALSA; // Import SALSA from the CrazyMinnow namespace
using IBM.Watson.DeveloperCloud.Services.Assistant.v1;
using FullSerializer;
using UnityEngine.UI;
using IBM.Watson.DeveloperCloud.Services.SpeechToText.v1;
using IBM.Watson.DeveloperCloud.DataTypes;



public class WatsonTTS : MonoBehaviour {
	public string myName;
	public RandomEyes3D randomEyes;
	public GameObject[] lookTargets;

	#region PLEASE SET THESE VARIABLES IN THE INSPECTOR
	[SerializeField]
	private string _STT_username;
	[SerializeField]
	private string _STT_password;
	[SerializeField]
	private string _STT_url;
	[SerializeField]
	private string _TTS_username;
	[SerializeField]
	private string _TTS_password;
	[SerializeField]
	private string _TTS_url;
	[SerializeField]
	private string _CONV_username;
	[SerializeField]
	private string _CONV_password;
	[SerializeField]
	private string _CONV_url;
	[SerializeField]
	private string _CONV_workspaceId;
	[SerializeField]
	private string _CONV_versionDate;
	#endregion
	//as this field is Public it is set in the Inspector
	public Text ResultsField;

	private int _recordingRoutine = 0;
	private string _microphoneID = null;
	private AudioClip _recording = null;
	private int _recordingBufferSize = 1;
	private int _recordingHZ = 22050;

	private SpeechToText _speechToText;
	private Assistant _service;
	private bool _messageTested = false;

	private fsSerializer _serializer = new fsSerializer();
	private Dictionary<string, object> _context = null;
	private bool _waitingForResponse = true;
	private float wait;
	private bool check;

	TextToSpeech _textToSpeech;
	private AudioClip audioClip; // Link an AudioClip for Salsa3D to play
	private Salsa3D salsa3D;
	private AudioSource audioSrc;

	private string TTS_content = "";
	public bool play = false;

	// Use this for initialization
	void Start () {

		LogSystem.InstallDefaultReactors();

		audioSrc = GetComponent<AudioSource>(); // Get the SALSA AudioSource from this GameObject

		//  Create credential and instantiate service
		Credentials CONVcredentials = new Credentials(_CONV_username, _CONV_password, _CONV_url);
		_service = new Assistant(CONVcredentials);
		_service.VersionDate = _CONV_versionDate;

		//  Create credential and instantiate service
		Credentials TTScredentials = new Credentials(_TTS_username, _TTS_password, _TTS_url);
		_textToSpeech = new TextToSpeech(TTScredentials);

		//  Create credential and instantiate service
		Credentials STTcredentials = new Credentials(_STT_username, _STT_password, _STT_url);
		_speechToText = new SpeechToText(STTcredentials);



		//  Message
		Dictionary<string, object> input = new Dictionary<string, object>();
		//Say HELLO - start the conversation
		input.Add("text", "first hello");
		MessageRequest messageRequest = new MessageRequest()
		{
			Input = input
		};
		_service.Message(OnCONVMessage, OnCONVFail, _CONV_workspaceId, messageRequest);
	
	
	}

	public bool Active
	{
		get { return _speechToText.IsListening; }
		set
		{
			if (value && !_speechToText.IsListening)
			{
				_speechToText.DetectSilence = true;
				_speechToText.EnableWordConfidence = true;
				_speechToText.EnableTimestamps = true;
				_speechToText.SilenceThreshold = 0.01f;
				_speechToText.MaxAlternatives = 0;
				_speechToText.EnableInterimResults = true;
				_speechToText.OnError = OnSTTError;
				_speechToText.InactivityTimeout = -1;
				_speechToText.ProfanityFilter = false;
				_speechToText.SmartFormatting = true;
				_speechToText.SpeakerLabels = false;
				_speechToText.WordAlternativesThreshold = null;
				_speechToText.StartListening(OnSTTRecognize, OnSTTRecognizeSpeaker);
			}
			else if (!value && _speechToText.IsListening)
			{
				_speechToText.StopListening();
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
//		Log.Debug("ExampleStreaming.RecordingHandler()", "devices: {0}", Microphone.devices);
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

				_speechToText.OnListen(record);

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

//	private void OnSTTRecognize(SpeechRecognitionEvent result)
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
//					Log.Debug("STT.OnSTTRecognize()", text);
					ResultsField.text = text;

					//only send to CONV once we know the user has stopped talking
					if (res.final) {
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
						_service.Message(OnCONVMessage, OnCONVFail, _CONV_workspaceId, messageRequest);

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
//	private void OnSTTRecognizeSpeaker(SpeakerRecognitionEvent result)
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
//		Log.Debug("Assistant.OnMessage()", "Response: {0}", customData["json"].ToString());
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

		//Need to wrap this in try/catch so don't trigger exception is has no content for some reason
		object _tempText = null;
		(messageResponse.Output as Dictionary<string, object>).TryGetValue ("text", out _tempText);
		object _tempTextObj = (_tempText as List<object>)[0];
		string output = _tempTextObj.ToString();

		if (output != null) {
//replace any <waitX> tags with the value expected by the TTS service
			string replaceActionTags = output.ToString();
			int pos3 = replaceActionTags.IndexOf("<wait3>");
			if(pos3 != -1) {
				replaceActionTags = output.Replace("<wait3>", "<break time='3s'/>");
			}
			int pos4 = replaceActionTags.IndexOf("<wait4>");
			if(pos4 != -1) {
				replaceActionTags = output.Replace("<wait4>", "<break time='4s'/>");
			}
			int pos5 = replaceActionTags.IndexOf("<wait5>");
			if(pos5 != -1) {
				replaceActionTags = output.Replace("<wait5>", "<break time='5s'/>");
			}	
			output = replaceActionTags;
		} else {
			Log.Debug("Extract outputText", "Failed to extract outputText and set for speaking");
		}

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
		//		Log.Debug("WatsonTTS", "Attempting synthesize.");
		_textToSpeech.Voice = VoiceType.en_US_Michael; // .en_US_Allison; //.en_GB_Kate;
		_textToSpeech.ToSpeech(HandleToSpeechCallback, OnTTSFail, TTS_content, true);
	}

	void HandleToSpeechCallback(AudioClip clip, Dictionary<string, object> customData = null)
	{
		if (Application.isPlaying && clip != null && audioSrc != null)
		{
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
	IEnumerator Look(float preDelay, float duration, GameObject lookTarget)
	{
		yield return new WaitForSeconds(preDelay);

		Debug.Log ("Look="+"LEFT/RIGHT");		
		randomEyes.SetLookTarget(lookTarget);

		yield return new WaitForSeconds(duration);

		randomEyes.SetLookTarget(null);
	}

	// Update is called once per frame
	void Update () {

		if (play)
		{
			Debug.Log ("play=true");
			play = false;
			Active = false;
			GetTTS();
		}

		if (check) {
//			Debug.Log ("Update() check=true");
			wait-=Time.deltaTime; //reverse count
		}

		if((wait<0f) && (check)) { //check that clip is not playing		
//			Debug.Log ("Speech has finished");
			check = false;
			//Now let's start listening again.....
			Active = true;
			StartRecording ();
		}
	}
}
