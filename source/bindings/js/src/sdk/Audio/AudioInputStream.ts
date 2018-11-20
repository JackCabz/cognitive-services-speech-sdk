// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

import { CreateNoDashGuid } from "../../../src/common/Guid";
import {
    AudioSourceEvent,
    AudioSourceInitializingEvent,
    AudioSourceReadyEvent,
    AudioStreamNodeAttachedEvent,
    AudioStreamNodeAttachingEvent,
    AudioStreamNodeDetachedEvent,
    Events,
    EventSource,
    IAudioSource,
    IAudioStreamNode,
    IStreamChunk,
    Promise,
    PromiseHelper,
    Stream,
    StreamReader,
} from "../../common/Exports";
import { AudioStreamFormat, PullAudioInputStreamCallback } from "../Exports";
import { AudioStreamFormatImpl } from "./AudioStreamFormat";

const bufferSize: number = 4096;

/**
 * Represents audio input stream used for custom audio input configurations.
 * @class AudioInputStream
 */
export abstract class AudioInputStream {

    /**
     * Creates and initializes an instance.
     * @constructor
     */
    protected constructor() { }

    /**
     * Creates a memory backed PushAudioInputStream with the specified audio format.
     * @member AudioInputStream.createPushStream
     * @function
     * @public
     * @param {AudioStreamFormat} format - The audio data format in which audio will be written to the push audio stream's write() method (currently only support 16 kHz 16bit mono PCM).
     * @returns {PushAudioInputStream} The audio input stream being created.
     */
    public static createPushStream(format?: AudioStreamFormat): PushAudioInputStream {
        return PushAudioInputStream.create(format);
    }

    /**
     * Creates a PullAudioInputStream that delegates to the specified callback interface for read() and close() methods.
     * @member AudioInputStream.createPullStream
     * @function
     * @public
     * @param {PullAudioInputStreamCallback} callback - The custom audio input object, derived from PullAudioInputStreamCallback
     * @param {AudioStreamFormat} format - The audio data format in which audio will be returned from the callback's read() method (currently only support 16 kHz 16bit mono PCM).
     * @returns {PullAudioInputStream} The audio input stream being created.
     */
    public static createPullStream(callback: PullAudioInputStreamCallback, format?: AudioStreamFormat): PullAudioInputStream {
        return PullAudioInputStream.create(callback, format);
        // throw new Error("Oops");
    }

    /**
     * Explicitly frees any external resource attached to the object
     * @member AudioInputStream.prototype.close
     * @function
     * @public
     */
    public abstract close(): void;
}

/**
 * Represents memory backed push audio input stream used for custom audio input configurations.
 * @class PushAudioInputStream
 */
// tslint:disable-next-line:max-classes-per-file
export abstract class PushAudioInputStream extends AudioInputStream {

    /**
     * Creates a memory backed PushAudioInputStream with the specified audio format.
     * @member PushAudioInputStream.create
     * @function
     * @public
     * @param {AudioStreamFormat} format - The audio data format in which audio will be written to the push audio stream's write() method (currently only support 16 kHz 16bit mono PCM).
     * @returns {PushAudioInputStream} The push audio input stream being created.
     */
    public static create(format?: AudioStreamFormat): PushAudioInputStream {
        return new PushAudioInputStreamImpl(format);
    }

    /**
     * Writes the audio data specified by making an internal copy of the data.
     * @member PushAudioInputStream.prototype.write
     * @function
     * @public
     * @param {ArrayBuffer} dataBuffer - The audio buffer of which this function will make a copy.
     */
    public abstract write(dataBuffer: ArrayBuffer): void;

    /**
     * Closes the stream.
     * @member PushAudioInputStream.prototype.close
     * @function
     * @public
     */
    public abstract close(): void;
}

/**
 * Represents memory backed push audio input stream used for custom audio input configurations.
 * @private
 * @class PushAudioInputStreamImpl
 */
// tslint:disable-next-line:max-classes-per-file
export class PushAudioInputStreamImpl extends PushAudioInputStream implements IAudioSource {

    private format: AudioStreamFormat;
    private id: string;
    private events: EventSource<AudioSourceEvent>;
    private stream: Stream<ArrayBuffer> = new Stream<ArrayBuffer>();

    /**
     * Creates and initalizes an instance with the given values.
     * @constructor
     * @param {AudioStreamFormat} format - The audio stream format.
     */
    public constructor(format?: AudioStreamFormat) {
        super();
        if (format === undefined) {
            this.format = AudioStreamFormatImpl.getDefaultInputFormat();
        } else {
            this.format = format;
        }
        this.events = new EventSource<AudioSourceEvent>();
        this.id = CreateNoDashGuid();
    }

    /**
     * Format information for the audio
     */
    public get Format(): AudioStreamFormat {
        return this.format;
    }

    /**
     * Writes the audio data specified by making an internal copy of the data.
     * @member PushAudioInputStreamImpl.prototype.write
     * @function
     * @public
     * @param {ArrayBuffer} dataBuffer - The audio buffer of which this function will make a copy.
     */
    public write(dataBuffer: ArrayBuffer): void {
        // Break the data up into smaller chunks if needed.
        let i: number;
        for (i = bufferSize - 1; i < dataBuffer.byteLength; i += bufferSize) {
            this.stream.Write(dataBuffer.slice(i - (bufferSize - 1), i + 1));
        }

        if ((i - (bufferSize - 1)) !== dataBuffer.byteLength) {
            this.stream.Write(dataBuffer.slice(i - (bufferSize - 1), dataBuffer.byteLength));
        }
    }

    /**
     * Closes the stream.
     * @member PushAudioInputStreamImpl.prototype.close
     * @function
     * @public
     */
    public close(): void {
        this.stream.Close();
    }

    public Id(): string {
        return this.id;
    }

    public TurnOn(): Promise<boolean> {
        this.OnEvent(new AudioSourceInitializingEvent(this.id)); // no stream id
        this.OnEvent(new AudioSourceReadyEvent(this.id));
        return PromiseHelper.FromResult(true);
    }

    public Attach(audioNodeId: string): Promise<IAudioStreamNode> {
        this.OnEvent(new AudioStreamNodeAttachingEvent(this.id, audioNodeId));

        return this.TurnOn()
            .OnSuccessContinueWith<StreamReader<ArrayBuffer>>((_: boolean) => {
                // For now we support a single parallel reader of the pushed stream.
                // So we can simiply hand the stream to the recognizer and let it recognize.

                return this.stream.GetReader();
            })
            .OnSuccessContinueWith((streamReader: StreamReader<ArrayBuffer>) => {
                this.OnEvent(new AudioStreamNodeAttachedEvent(this.id, audioNodeId));

                return {
                    Detach: () => {
                        streamReader.Close();
                        this.OnEvent(new AudioStreamNodeDetachedEvent(this.id, audioNodeId));
                        this.TurnOff();
                    },
                    Id: () => {
                        return audioNodeId;
                    },
                    Read: () => {
                        return streamReader.Read();
                    },
                };
            });
    }

    public Detach(audioNodeId: string): void {
        this.OnEvent(new AudioStreamNodeDetachedEvent(this.id, audioNodeId));
    }

    public TurnOff(): Promise<boolean> {
        return PromiseHelper.FromResult(false);
    }

    public get Events(): EventSource<AudioSourceEvent> {
        return this.events;
    }

    private OnEvent = (event: AudioSourceEvent): void => {
        this.events.OnEvent(event);
        Events.Instance.OnEvent(event);
    }
}

/*
 * Represents audio input stream used for custom audio input configurations.
 * @class PullAudioInputStream
 */
// tslint:disable-next-line:max-classes-per-file
export abstract class PullAudioInputStream extends AudioInputStream {
    /**
     * Creates and initializes and instance.
     * @constructor
     */
    protected constructor() { super(); }

    /**
     * Creates a PullAudioInputStream that delegates to the specified callback interface for read() and close() methods, using the default format (16 kHz 16bit mono PCM).
     * @member PullAudioInputStream.create
     * @function
     * @public
     * @param {PullAudioInputStreamCallback} callback - The custom audio input object, derived from PullAudioInputStreamCustomCallback
     * @param {AudioStreamFormat} format - The audio data format in which audio will be returned from the callback's read() method (currently only support 16 kHz 16bit mono PCM).
     * @returns {PullAudioInputStream} The push audio input stream being created.
     */
    public static create(callback: PullAudioInputStreamCallback, format?: AudioStreamFormat): PullAudioInputStream {
        return new PullAudioInputStreamImpl(callback, format);
    }

    /**
     * Explicitly frees any external resource attached to the object
     * @member PullAudioInputStream.prototype.close
     * @function
     * @public
     */
    public abstract close(): void;

}

/**
 * Represents audio input stream used for custom audio input configurations.
 * @private
 * @class PullAudioInputStreamImpl
 */
// tslint:disable-next-line:max-classes-per-file
export class PullAudioInputStreamImpl extends PullAudioInputStream implements IAudioSource {

    private callback: PullAudioInputStreamCallback;
    private format: AudioStreamFormat;
    private id: string;
    private events: EventSource<AudioSourceEvent>;
    private isClosed: boolean;

    /**
     * Creates a PullAudioInputStream that delegates to the specified callback interface for read() and close() methods, using the default format (16 kHz 16bit mono PCM).
     * @constructor
     * @param {PullAudioInputStreamCallback} callback - The custom audio input object, derived from PullAudioInputStreamCustomCallback
     * @param {AudioStreamFormat} format - The audio data format in which audio will be returned from the callback's read() method (currently only support 16 kHz 16bit mono PCM).
     */
    public constructor(callback: PullAudioInputStreamCallback, format?: AudioStreamFormat) {
        super();
        if (undefined === format) {
            this.format = AudioStreamFormat.getDefaultInputFormat();
        } else {
            this.format = format;
        }
        this.events = new EventSource<AudioSourceEvent>();
        this.id = CreateNoDashGuid();
        this.callback = callback;
        this.isClosed = false;
    }

    /**
     * Format information for the audio
     */
    public get Format(): AudioStreamFormat {
        return this.format;
    }

    /**
     * Closes the stream.
     * @member PullAudioInputStreamImpl.prototype.close
     * @function
     * @public
     */
    public close(): void {
        this.isClosed = true;
        this.callback.close();
    }

    public Id(): string {
        return this.id;
    }

    public TurnOn(): Promise<boolean> {
        this.OnEvent(new AudioSourceInitializingEvent(this.id)); // no stream id
        this.OnEvent(new AudioSourceReadyEvent(this.id));
        return PromiseHelper.FromResult(true);
    }

    public Attach(audioNodeId: string): Promise<IAudioStreamNode> {
        this.OnEvent(new AudioStreamNodeAttachingEvent(this.id, audioNodeId));

        return this.TurnOn()
            .OnSuccessContinueWith((result: boolean) => {
                this.OnEvent(new AudioStreamNodeAttachedEvent(this.id, audioNodeId));

                return {
                    Detach: () => {
                        this.callback.close();
                        this.OnEvent(new AudioStreamNodeDetachedEvent(this.id, audioNodeId));
                        this.TurnOff();
                    },
                    Id: () => {
                        return audioNodeId;
                    },
                    Read: (): Promise<IStreamChunk<ArrayBuffer>> => {
                        const readBuff: ArrayBuffer = new ArrayBuffer(bufferSize);
                        const pulledBytes: number = this.callback.read(readBuff);

                        return PromiseHelper.FromResult<IStreamChunk<ArrayBuffer>>({
                            Buffer: readBuff.slice(0, pulledBytes),
                            IsEnd: this.isClosed,
                        });
                    },
                };
            });

    }

    public Detach(audioNodeId: string): void {
        this.OnEvent(new AudioStreamNodeDetachedEvent(this.id, audioNodeId));
    }

    public TurnOff(): Promise<boolean> {
        return PromiseHelper.FromResult(false);
    }

    public get Events(): EventSource<AudioSourceEvent> {
        return this.events;
    }

    private OnEvent = (event: AudioSourceEvent): void => {
        this.events.OnEvent(event);
        Events.Instance.OnEvent(event);
    }
}
