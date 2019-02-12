//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Microsoft.CognitiveServices.Speech
{
    /// <summary>
    /// Performs speech recognition from microphone, file, or other audio input streams, and gets transcribed text as result.
    /// </summary>
    /// <example>
    /// An example to use the speech recognizer from microphone and listen to events generated by the recognizer.
    /// <code>
    /// public async Task SpeechContinuousRecognitionAsync()
    /// {
    ///     // Creates an instance of a speech config with specified subscription key and service region.
    ///     // Replace with your own subscription key and service region (e.g., "westus").
    ///     var config = SpeechConfig.FromSubscription("YourSubscriptionKey", "YourServiceRegion");
    ///
    ///     // Creates a speech recognizer from microphone.
    ///     using (var recognizer = new SpeechRecognizer(config))
    ///     {
    ///         // Subscribes to events.
    ///         recognizer.Recognizing += (s, e) => {
    ///             Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
    ///         };
    ///
    ///         recognizer.Recognized += (s, e) => {
    ///             var result = e.Result;
    ///             Console.WriteLine($"Reason: {result.Reason.ToString()}");
    ///             if (result.Reason == ResultReason.RecognizedSpeech)
    ///             {
    ///                     Console.WriteLine($"Final result: Text: {result.Text}.");
    ///             }
    ///         };
    ///
    ///         recognizer.Canceled += (s, e) => {
    ///             Console.WriteLine($"\n    Recognition Canceled. Reason: {e.Reason.ToString()}, CanceledReason: {e.Reason}");
    ///         };
    ///
    ///         recognizer.SessionStarted += (s, e) => {
    ///             Console.WriteLine("\n    Session started event.");
    ///         };
    ///
    ///         recognizer.SessionStopped += (s, e) => {
    ///             Console.WriteLine("\n    Session stopped event.");
    ///         };
    ///
    ///         // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
    ///         await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
    ///
    ///         do
    ///         {
    ///             Console.WriteLine("Press Enter to stop");
    ///         } while (Console.ReadKey().Key != ConsoleKey.Enter);
    ///
    ///         // Stops recognition.
    ///         await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
    ///     }
    /// }
    /// </code>
    /// </example>
    public sealed class SpeechRecognizer : Recognizer
    {
        /// <summary>
        /// The event <see cref="Recognizing"/> signals that an intermediate recognition result is received.
        /// </summary>
        public event EventHandler<SpeechRecognitionEventArgs> Recognizing;

        /// <summary>
        /// The event <see cref="Recognized"/> signals that a final recognition result is received.
        /// </summary>
        public event EventHandler<SpeechRecognitionEventArgs> Recognized;

        /// <summary>
        /// The event <see cref="Canceled"/> signals that the speech recognition was canceled.
        /// </summary>
        public event EventHandler<SpeechRecognitionCanceledEventArgs> Canceled;

        private Internal.CallbackFunctionDelegate recognizingCallbackDelegate;
        private Internal.CallbackFunctionDelegate recognizedCallbackDelegate;
        private Internal.CallbackFunctionDelegate canceledCallbackDelegate;

        /// <summary>
        /// Creates a new instance of SpeechRecognizer.
        /// </summary>
        /// <param name="speechConfig">Speech configuration</param>
        public SpeechRecognizer(SpeechConfig speechConfig)
            : this(speechConfig != null ? speechConfig.configImpl : throw new ArgumentNullException(nameof(speechConfig)), null)
        {
        }

        /// <summary>
        /// Creates a new instance of SpeechRecognizer.
        /// </summary>
        /// <param name="speechConfig">Speech configuration</param>
        /// <param name="audioConfig">Audio configuration</param>
        public SpeechRecognizer(SpeechConfig speechConfig, Audio.AudioConfig audioConfig)
            : this(speechConfig != null ? speechConfig.configImpl : throw new ArgumentNullException(nameof(speechConfig)),
                   audioConfig != null ? audioConfig.configImpl : throw new ArgumentNullException(nameof(audioConfig)))
        {
            this.audioConfig = audioConfig;
        }

        internal SpeechRecognizer(Internal.SpeechConfig config, Internal.AudioConfig audioConfig)
            : this(Internal.SpeechRecognizer.FromConfig(config, audioConfig))
        {
        }

        internal SpeechRecognizer(Internal.SpeechRecognizer recoImpl) : base(recoImpl)
        {
            this.recoImpl = recoImpl;

            recognizingCallbackDelegate = FireEvent_Recognizing;
            recognizedCallbackDelegate = FireEvent_Recognized;
            canceledCallbackDelegate = FireEvent_Canceled;

            recoImpl.SetRecognizingCallback(recognizingCallbackDelegate, GCHandle.ToIntPtr(gch));
            recoImpl.SetRecognizedCallback(recognizedCallbackDelegate, GCHandle.ToIntPtr(gch));
            recoImpl.SetCanceledCallback(canceledCallbackDelegate, GCHandle.ToIntPtr(gch));

            Properties = new PropertyCollection(recoImpl.Properties);
        }

        /// <summary>
        /// Gets the endpoint ID of a customized speech model that is used for speech recognition.
        /// </summary>
        /// <returns>the endpoint ID of a customized speech model that is used for speech recognition</returns>
        public string EndpointId
        {
            get
            {
                return this.recoImpl.EndpointId;
            }
        }

        /// <summary>
        /// Gets/sets authorization token used to communicate with the service.
        /// Note: The caller needs to ensure that the authorization token is valid. Before the authorization token
        /// expires, the caller needs to refresh it by calling this setter with a new valid token.
        /// Otherwise, the recognizer will encounter errors during recognition.
        /// </summary>
        public string AuthorizationToken
        {
            get
            {
                return this.recoImpl.AuthorizationToken;
            }

            set
            {
                if(value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                this.recoImpl.AuthorizationToken = value;
            }
        }

        /// <summary>
        /// Gets the language name that was set when the recognizer was created.
        /// </summary>
        public string SpeechRecognitionLanguage
        {
            get
            {
                return this.recoImpl.Properties.GetProperty(Internal.PropertyId.SpeechServiceConnection_RecoLanguage, string.Empty);
            }
        }

        /// <summary>
        /// Gets the output format setting.
        /// </summary>
        public OutputFormat OutputFormat
        {
            get
            {
                return this.recoImpl.Properties.GetProperty(Internal.PropertyId.SpeechServiceResponse_RequestDetailedResultTrueFalse, "false") == "true"
                    ? OutputFormat.Detailed
                    : OutputFormat.Simple;
            }
        }

        /// <summary>
        /// The collection or properties and their values defined for this <see cref="SpeechRecognizer"/>.
        /// </summary>
        public PropertyCollection Properties { get; internal set; }

        /// <summary>
        /// Starts speech recognition, and stops after the first utterance is recognized. The task returns the recognition text as result.
        /// Note: RecognizeOnceAsync() returns when the first utterance has been recognized, so it is suitable only for single shot recognition like command or query. For long-running recognition, use StartContinuousRecognitionAsync() instead.
        /// </summary>
        /// <returns>A task representing the recognition operation. The task returns a value of <see cref="SpeechRecognitionResult"/> </returns>
        /// <example>
        /// The following example creates a speech recognizer, and then gets and prints the recognition result.
        /// <code>
        /// public async Task SpeechSingleShotRecognitionAsync()
        /// {
        ///     // Creates an instance of a speech config with specified subscription key and service region.
        ///     // Replace with your own subscription key and service region (e.g., "westus").
        ///     var config = SpeechConfig.FromSubscription("YourSubscriptionKey", "YourServiceRegion");
        ///
        ///     // Creates a speech recognizer using microphone as audio input. The default language is "en-us".
        ///     using (var recognizer = new SpeechRecognizer(config))
        ///     {
        ///         Console.WriteLine("Say something...");
        ///
        ///         // Performs recognition. RecognizeOnceAsync() returns when the first utterance has been recognized,
        ///         // so it is suitable only for single shot recognition like command or query. For long-running
        ///         // recognition, use StartContinuousRecognitionAsync() instead.
        ///         var result = await recognizer.RecognizeOnceAsync();
        ///
        ///         // Checks result.
        ///         if (result.Reason == ResultReason.RecognizedSpeech)
        ///         {
        ///             Console.WriteLine($"RECOGNIZED: Text={result.Text}");
        ///         }
        ///         else if (result.Reason == ResultReason.NoMatch)
        ///         {
        ///             Console.WriteLine($"NOMATCH: Speech could not be recognized.");
        ///         }
        ///         else if (result.Reason == ResultReason.Canceled)
        ///         {
        ///             var cancellation = CancellationDetails.FromResult(result);
        ///             Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");
        ///
        ///             if (cancellation.Reason == CancellationReason.Error)
        ///             {
        ///                 Console.WriteLine($"CANCELED: ErrorCode={cancelation.ErrorCode}");
        ///                 Console.WriteLine($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
        ///                 Console.WriteLine($"CANCELED: Did you update the subscription info?");
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        /// </example>
        public Task<SpeechRecognitionResult> RecognizeOnceAsync()
        {
            return Task.Run(() =>
            {
                SpeechRecognitionResult result = null;
                base.DoAsyncRecognitionAction(() => result = new SpeechRecognitionResult(this.recoImpl.RecognizeOnce()));
                return result;
            });
        }

        /// <summary>
        /// Starts speech recognition on a continuous audio stream, until StopContinuousRecognitionAsync() is called.
        /// User must subscribe to events to receive recognition results.
        /// </summary>
        /// <returns>A task representing the asynchronous operation that starts the recognition.</returns>
        public Task StartContinuousRecognitionAsync()
        {
            return Task.Run(() =>
            {
                base.DoAsyncRecognitionAction(this.recoImpl.StartContinuousRecognition);
            });
        }

        /// <summary>
        /// Stops continuous speech recognition.
        /// </summary>
        /// <returns>A task representing the asynchronous operation that stops the recognition.</returns>
        public Task StopContinuousRecognitionAsync()
        {
            return Task.Run(() =>
            {
                base.DoAsyncRecognitionAction(this.recoImpl.StopContinuousRecognition);
            });
        }

        /// <summary>
        /// Starts speech recognition on a continuous audio stream with keyword spotting, until StopKeywordRecognitionAsync() is called.
        /// User must subscribe to events to receive recognition results.
        /// Note: Keyword spotting functionality is only available on the Cognitive Services Device SDK. This functionality is currently not included in the SDK itself.
        /// </summary>
        /// <param name="model">The keyword recognition model that specifies the keyword to be recognized.</param>
        /// <returns>A task representing the asynchronous operation that starts the recognition.</returns>
        public Task StartKeywordRecognitionAsync(KeywordRecognitionModel model)
        {
            return Task.Run(() =>
            {
                base.DoAsyncRecognitionAction(() => this.recoImpl.StartKeywordRecognition(model.modelImpl));
            });
        }

        /// <summary>
        /// Stops continuous speech recognition with keyword spotting.
        /// Note: Keyword spotting functionality is only available on the Cognitive Services Device SDK. This functionality is currently not included in the SDK itself.
        /// </summary>
        /// <returns>A task representing the asynchronous operation that stops the recognition.</returns>
        public Task StopKeywordRecognitionAsync()
        {
            return Task.Run(() =>
            {
                base.DoAsyncRecognitionAction(this.recoImpl.StopKeywordRecognition);
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    recoImpl.SetRecognizingCallback(null, GCHandle.ToIntPtr(gch));
                    recoImpl.SetRecognizedCallback(null, GCHandle.ToIntPtr(gch));
                    recoImpl.SetCanceledCallback(null, GCHandle.ToIntPtr(gch));
                    recoImpl.SetSessionStartedCallback(null, GCHandle.ToIntPtr(gch));
                    recoImpl.SetSessionStoppedCallback(null, GCHandle.ToIntPtr(gch));
                    recoImpl.SetSpeechStartDetectedCallback(null, GCHandle.ToIntPtr(gch));
                    recoImpl.SetSpeechEndDetectedCallback(null, GCHandle.ToIntPtr(gch));
                }
                catch (ApplicationException e)
                {
                    Internal.SpxExceptionThrower.LogError(e.Message);
                }

                recoImpl?.Dispose();

                recognizingCallbackDelegate = null;
                recognizedCallbackDelegate = null;
                canceledCallbackDelegate = null;

                base.Dispose(disposing);
            }
        }

        private readonly new Internal.SpeechRecognizer recoImpl;
        private readonly Audio.AudioConfig audioConfig;

        // Defines a private methods to raise a C# event for intermediate/final result when a corresponding callback is invoked by the native layer.

        [Internal.MonoPInvokeCallback]
        private static void FireEvent_Recognizing(IntPtr hreco, IntPtr hevent, IntPtr pvContext)
        {
            try
            {
                EventHandler<SpeechRecognitionEventArgs> eventHandle;
                SpeechRecognizer recognizer = (SpeechRecognizer)GCHandle.FromIntPtr(pvContext).Target;
                lock (recognizer.recognizerLock)
                {
                    if (recognizer.isDisposing) return;
                    eventHandle = recognizer.Recognizing;
                }
                var eventArgs = new Internal.SpeechRecognitionEventArgs(hevent);
                var resultEventArg = new SpeechRecognitionEventArgs(eventArgs);
                eventHandle?.Invoke(recognizer, resultEventArg);
            }
            catch (InvalidOperationException)
            {
                Internal.SpxExceptionThrower.LogError(Internal.SpxError.InvalidHandle);
            }
        }

        [Internal.MonoPInvokeCallback]
        private static void FireEvent_Recognized(IntPtr hreco, IntPtr hevent, IntPtr pvContext)
        {
            try
            {
                EventHandler<SpeechRecognitionEventArgs> eventHandle;
                SpeechRecognizer recognizer = (SpeechRecognizer)GCHandle.FromIntPtr(pvContext).Target;
                lock (recognizer.recognizerLock)
                {
                    if (recognizer.isDisposing) return;
                    eventHandle = recognizer.Recognized;
                }
                var eventArgs = new Internal.SpeechRecognitionEventArgs(hevent);
                var resultEventArg = new SpeechRecognitionEventArgs(eventArgs);
                eventHandle?.Invoke(recognizer, resultEventArg);
            }
            catch (InvalidOperationException)
            {
                Internal.SpxExceptionThrower.LogError(Internal.SpxError.InvalidHandle);
            }
        }

        [Internal.MonoPInvokeCallback]
        private static void FireEvent_Canceled(IntPtr hreco, IntPtr hevent, IntPtr pvContext)
        {
            try
            {
                EventHandler<SpeechRecognitionCanceledEventArgs> eventHandle;
                SpeechRecognizer recognizer = (SpeechRecognizer)GCHandle.FromIntPtr(pvContext).Target;
                lock (recognizer.recognizerLock)
                {
                    if (recognizer.isDisposing) return;
                    eventHandle = recognizer.Canceled;
                }
                var eventArgs = new Internal.SpeechRecognitionCanceledEventArgs(hevent);
                var resultEventArg = new SpeechRecognitionCanceledEventArgs(eventArgs);
                eventHandle?.Invoke(recognizer, resultEventArg);
            }
            catch (InvalidOperationException)
            {
                Internal.SpxExceptionThrower.LogError(Internal.SpxError.InvalidHandle);
            }
        }
    }
}
