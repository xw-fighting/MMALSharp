﻿// <copyright file="MMALPortBase.cs" company="Techyian">
// Copyright (c) Techyian. All rights reserved.
// Licensed under the MIT License. Please see LICENSE.txt for License info.
// </copyright>

using System;
using System.Runtime.InteropServices;
using MMALSharp.Native;
using MMALSharp.Components;
using MMALSharp.Handlers;
using static MMALSharp.MMALCallerHelper;
using Nito.AsyncEx;

namespace MMALSharp
{
    public enum PortType
    {
        Input,
        Output,
        Clock,
        Control,
        Unknown
    }

    /// <summary>
    /// Base class for port objects
    /// </summary>
    public abstract unsafe class MMALPortBase : MMALObject
    {
        /// <summary>
        /// Native pointer that represents this port
        /// </summary>
        internal MMAL_PORT_T* Ptr { get; set; }

        /// <summary>
        /// Native pointer that represents the component this port is associated with
        /// </summary>
        internal MMAL_COMPONENT_T* Comp { get; set; }

        /// <summary>
        /// Specifies the type of port this is
        /// </summary>
        public PortType PortType { get; set; }

        /// <summary>
        /// Managed reference to the component this port is associated with
        /// </summary>
        public MMALComponentBase ComponentReference { get; set; }

        /// <summary>
        /// Managed reference to the downstream component this port is connected to
        /// </summary>
        public MMALConnectionImpl ConnectedReference { get; set; }

        /// <summary>
        /// Managed reference to the pool of buffer headers associated with this port
        /// </summary>
        public MMALPoolImpl BufferPool { get; set; }

        /// <summary>
        /// Managed name given to this object (user defined)
        /// </summary>
        public string ObjName { get; set; }

        public MMALEncoding EncodingType { get; set; }

        public MMALEncoding PixelFormat { get; set; }

        /// <summary>
        /// Native name of port
        /// </summary>
        public string Name => Marshal.PtrToStringAnsi((IntPtr)(this.Ptr->Name));

        /// <summary>
        /// Indicates whether this port is enabled
        /// </summary>
        public bool Enabled => this.Ptr->IsEnabled == 1;

        /// <summary>
        /// Specifies minimum number of buffer headers required for this port 
        /// </summary>
        public int BufferNumMin => this.Ptr->BufferNumMin;
        
        /// <summary>
        /// Specifies minimum size of buffer headers required for this port
        /// </summary>
        public uint BufferSizeMin => this.Ptr->BufferSizeMin;

        /// <summary>
        /// Specifies minimum alignment value for buffer headers required for this port
        /// </summary>
        public int BufferAlignmentMin => this.Ptr->BufferAlignmentMin;

        /// <summary>
        /// Specifies recommended number of buffer headers for this port
        /// </summary>
        public int BufferNumRecommended => this.Ptr->BufferNumRecommended;

        /// <summary>
        /// Specifies recommended size of buffer headers for this port
        /// </summary>
        public uint BufferSizeRecommended => this.Ptr->BufferSizeRecommended;

        /// <summary>
        /// Indicates the currently set number of buffer headers for this port
        /// </summary>
        public int BufferNum
        {
            get
            {
                return this.Ptr->BufferNum;
            }
            set
            {
                this.Ptr->BufferNum = value;
            }
        }

        /// <summary>
        /// Indicates the currently set size of buffer headers for this port
        /// </summary>
        public uint BufferSize
        {
            get
            {
                return this.Ptr->BufferSize;
            }
            set
            {
                this.Ptr->BufferSize = value;
            }
        }

        /// <summary>
        /// Accessor for the elementary stream
        /// </summary>
        public MMAL_ES_FORMAT_T Format => *this.Ptr->Format;

        public int Width => this.Ptr->Format->es->video.width;

        public int Height => this.Ptr->Format->es->video.height;

        public int CropWidth => this.Ptr->Format->es->video.crop.Width;

        public int CropHeight => this.Ptr->Format->es->video.crop.Height;

        public int NativeEncodingType => this.Ptr->Format->encoding;

        public int NativeEncodingSubformat => this.Ptr->Format->encodingVariant;
        
        /// <summary>
        /// Asynchronous trigger which is set when processing has completed on this port.
        /// </summary>
        public AsyncCountdownEvent Trigger { get; set; }

        /// <summary>
        /// Monitor lock for input port callback method
        /// </summary>
        protected static object InputLock = new object();

        /// <summary>
        /// Monitor lock for output port callback method
        /// </summary>
        protected static object OutputLock = new object();

        /// <summary>
        /// Monitor lock for control port callback method
        /// </summary>
        protected static object ControlLock = new object();

        /// <summary>
        /// Delegate for native port callback
        /// </summary>
        internal MMALSharp.Native.MMALPort.MMAL_PORT_BH_CB_T NativeCallback { get; set; }

        /// <summary>
        /// Delegate to populate native buffer header with user provided image data
        /// </summary>
        public Func<MMALBufferImpl, MMALPortBase, ProcessResult> ManagedInputCallback { get; set; }

        /// <summary>
        /// Delegate we use to do further processing on buffer headers when they're received by the native callback delegate
        /// </summary>
        public Action<MMALBufferImpl, MMALPortBase> ManagedOutputCallback { get; set; }

        /// <summary>
        /// Managed Control port callback delegate
        /// </summary>
        public Action<MMALBufferImpl, MMALPortBase> ManagedControlCallback { get; set; }

        protected MMALPortBase(MMAL_PORT_T* ptr, MMALComponentBase comp, PortType type)
        {
            this.Ptr = ptr;
            this.Comp = ptr->Component;
            this.ComponentReference = comp;
            this.PortType = type;
        }

        /// <summary>
        /// Connects two components together by their input and output ports
        /// </summary>
        /// <param name="destinationComponent">The component we want to connect to</param>
        /// <param name="inputPort">The input port of the component we want to connect to</param>
        /// <param name="useCallback">Flag to use connection callback (adversely affects performance)</param>
        /// <returns>The input port of the component we're connecting to - allows chain calling of this method</returns>
        public MMALPortBase ConnectTo(MMALDownstreamComponent destinationComponent, int inputPort = 0, bool useCallback = false)
        {
            if (this.ConnectedReference != null)
            {
                MMALLog.Logger.Warn("A connection has already been established on this port");
                return destinationComponent.Inputs[inputPort];
            }

            var connection = MMALConnectionImpl.CreateConnection(this, destinationComponent.Inputs[inputPort], destinationComponent, useCallback);
            this.ConnectedReference = connection;
            destinationComponent.Inputs[inputPort].ConnectedReference = connection;

            return destinationComponent.Inputs[inputPort];
        }

        public MMALPortBase ConnectTo(MMALDownstreamComponent destinationComponent, int inputPort, Func<MMALPortBase> callback)
        {
            this.ConnectTo(destinationComponent, inputPort);
            callback();
            return destinationComponent.Inputs[inputPort];
        }

        /// <summary>
        /// Represents the native callback method for an input port that's called by MMAL
        /// </summary>
        /// <param name="port">Native port struct pointer</param>
        /// <param name="buffer">Native buffer header pointer</param>
        internal virtual void NativeInputPortCallback(MMAL_PORT_T* port, MMAL_BUFFER_HEADER_T* buffer)
        {
        }

        /// <summary>
        /// Represents the native callback method for an output port that's called by MMAL
        /// </summary>
        /// <param name="port">Native port struct pointer</param>
        /// <param name="buffer">Native buffer header pointer</param>
        internal virtual void NativeOutputPortCallback(MMAL_PORT_T* port, MMAL_BUFFER_HEADER_T* buffer)
        {
        }

        /// <summary>
        /// Represents the native callback method for a control port that's called by MMAL
        /// </summary>
        /// <param name="port">Native port struct pointer</param>
        /// <param name="buffer">Native buffer header pointer</param>
        internal virtual void NativeControlPortCallback(MMAL_PORT_T* port, MMAL_BUFFER_HEADER_T* buffer) { }

        /// <summary>
        /// Provides functionality to enable processing on an output port.
        /// </summary>
        /// <param name="managedCallback">Delegate for managed output port callback</param>
        internal virtual void EnablePort(Action<MMALBufferImpl, MMALPortBase> managedCallback)
        {
            if (managedCallback != null)
            {
                this.SendAllBuffers();
            }
        }

        /// <summary>
        /// Provides functionality to enable processing on an input port.
        /// </summary>
        /// <param name="managedCallback">Delegate for managed input port callback</param>
        internal virtual void EnablePort(Func<MMALBufferImpl, MMALPortBase, ProcessResult> managedCallback)
        {
            // We populate the input buffers with user provided data.
            if (managedCallback != null)
            {
                this.BufferPool = new MMALPoolImpl(this);

                var length = this.BufferPool.Queue.QueueLength();

                for (int i = 0; i < length; i++)
                {
                    MMALBufferImpl buffer = this.BufferPool.Queue.GetBuffer();

                    ProcessResult result = managedCallback(buffer, this);

                    buffer.ReadIntoBuffer(result.BufferFeed, result.DataLength, result.EOF);

                    MMALLog.Logger.Debug($"Sending buffer to input port: Length {buffer.Length}");

                    if (result.DataLength > 0)
                    {
                        this.SendBuffer(buffer);
                    }
                    else
                    {
                        MMALLog.Logger.Debug("Data length is empty. Releasing buffer.");
                        buffer.Release();
                        buffer.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Disable processing on a port. Disabling a port will stop all processing on this port and return all (non-processed)
        /// buffer headers to the client. If this is a connected output port, the input port to which it is connected shall also be disabled.
        /// Any buffer pool shall be released.
        /// </summary>
        internal void DisablePort()
        {
            if (this.Enabled)
            {
                MMALLog.Logger.Debug("Disabling port");

                if (this.BufferPool != null)
                {
                    var length = this.BufferPool.Queue.QueueLength();

                    MMALLog.Logger.Debug($"Releasing {length} buffers from queue.");

                    for (int i = 0; i < length; i++)
                    {
                        MMALLog.Logger.Debug("Releasing active buffer");
                        var buffer = this.BufferPool.Queue.GetBuffer();
                        buffer.Release();
                    }
                }

                MMALCheck(MMALPort.mmal_port_disable(this.Ptr), "Unable to disable port.");
            }
        }

        /// <summary>
        /// Commit format changes on this port.
        /// </summary>
        internal void Commit()
        {
            MMALCheck(MMALPort.mmal_port_format_commit(this.Ptr), "Unable to commit port changes.");
        }

        /// <summary>
        /// Shallow copy a format structure. It is worth noting that the extradata buffer will not be copied in the new format.
        /// </summary>
        /// <param name="destination">The destination port we're copying to</param>
        internal void ShallowCopy(MMALPortBase destination)
        {
            MMALFormat.mmal_format_copy(destination.Ptr->Format, this.Ptr->Format);
        }

        /// <summary>
        /// Fully copy a format structure, including the extradata buffer.
        /// </summary>
        /// <param name="destination">The destination port we're copying to</param>
        internal void FullCopy(MMALPortBase destination)
        {
            MMALFormat.mmal_format_full_copy(destination.Ptr->Format, this.Ptr->Format);
        }

        /// <summary>
        /// Ask a port to release all the buffer headers it currently has. This is an asynchronous operation and the
        /// flush call will return before all the buffer headers are returned to the client.
        /// </summary>
        internal void Flush()
        {
            MMALCheck(MMALPort.mmal_port_flush(this.Ptr), "Unable to flush port.");
        }

        /// <summary>
        /// Send a buffer header to a port.
        /// </summary>
        /// <param name="buffer">A managed buffer object</param>
        internal void SendBuffer(MMALBufferImpl buffer)
        {
            MMALCheck(MMALPort.mmal_port_send_buffer(this.Ptr, buffer.Ptr), "Unable to send buffer header.");
        }

        internal void SendAllBuffers()
        {
            this.BufferPool = new MMALPoolImpl(this);

            var length = this.BufferPool.Queue.QueueLength();

            for (int i = 0; i < length; i++)
            {
                var buffer = this.BufferPool.Queue.GetBuffer();

                MMALLog.Logger.Debug($"Sending buffer to output port: Length {buffer.Length}");

                this.SendBuffer(buffer);
            }
        }

        /// <summary>
        /// Destroy a pool of MMAL_BUFFER_HEADER_T associated with a specific port. This will also deallocate all of the memory
        /// which was allocated when creating or resizing the pool.
        /// </summary>
        internal void DestroyPortPool()
        {
            if (this.BufferPool != null)
            {
                if (this.Enabled)
                {
                    this.DisablePort();
                }

                MMALUtil.mmal_port_pool_destroy(this.Ptr, this.BufferPool.Ptr);
            }
        }

        /// <summary>
        /// Releases an input port buffer and reads further data from user provided image data if not reached end of file
        /// </summary>
        /// <param name="bufferImpl">A managed buffer object</param>
        internal void ReleaseInputBuffer(MMALBufferImpl bufferImpl)
        {
            MMALLog.Logger.Debug("Releasing input buffer.");

            bufferImpl.Release();
            bufferImpl.Dispose();

            if (this.Enabled && this.BufferPool != null)
            {
                var newBuffer = MMALQueueImpl.GetBuffer(this.BufferPool.Queue.Ptr);

                // Populate the new input buffer with user provided image data.
                var result = this.ManagedInputCallback(newBuffer, this);
                bufferImpl.ReadIntoBuffer(result.BufferFeed, result.DataLength, result.EOF);

                try
                {
                    if (this.Trigger != null && this.Trigger.CurrentCount > 0 && result.EOF)
                    {
                        MMALLog.Logger.Debug("Received EOF. Releasing.");

                        this.Trigger.Signal();
                        newBuffer.Release();
                        newBuffer.Dispose();
                        newBuffer = null;
                    }

                    if (newBuffer != null)
                    {
                        this.SendBuffer(newBuffer);
                    }
                    else
                    {
                        MMALLog.Logger.Warn("Buffer null. Continuing.");
                    }
                }
                catch (Exception ex)
                {
                    MMALLog.Logger.Warn($"Buffer handling failed. {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Release an output port buffer, get a new one from the queue and send it for processing
        /// </summary>
        /// <param name="bufferImpl">A managed buffer object</param>
        internal void ReleaseOutputBuffer(MMALBufferImpl bufferImpl)
        {
            bufferImpl.Release();
            bufferImpl.Dispose();
            try
            {
                if (!this.Enabled)
                {
                    MMALLog.Logger.Warn("Port not enabled.");
                }

                if (this.BufferPool == null)
                {
                    MMALLog.Logger.Warn("Buffer pool null.");
                }

                if (this.Enabled && this.BufferPool != null)
                {
                    var newBuffer = MMALQueueImpl.GetBuffer(this.BufferPool.Queue.Ptr);

                    if (newBuffer != null)
                    {
                        this.SendBuffer(newBuffer);
                    }
                    else
                    {
                        MMALLog.Logger.Warn("Buffer null. Continuing.");
                    }
                }
            }
            catch (Exception e)
            {
                MMALLog.Logger.Warn($"Unable to send buffer header. {e.Message}");
                throw;
            }
        }                
    }
}
