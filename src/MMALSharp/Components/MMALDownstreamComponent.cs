﻿// <copyright file="MMALDownstreamComponent.cs" company="Techyian">
// Copyright (c) Techyian. All rights reserved.
// Licensed under the MIT License. Please see LICENSE.txt for License info.
// </copyright>

using System;
using System.Collections.Generic;
using MMALSharp.Native;

namespace MMALSharp.Components
{
    /// <summary>
    /// Represents a downstream component. A downstream component is a component that can have data passed to it from further up the component
    /// heirarchy.
    /// </summary>
    public abstract class MMALDownstreamComponent : MMALComponentBase
    {
        protected MMALDownstreamComponent(string name)
            : base(name)
        {
            MMALCamera.Instance.DownstreamComponents.Add(this);
            this.ProcessingPorts = new List<int>();
        }

        public abstract int Width { get; set; }

        public abstract int Height { get; set; }

        public List<int> ProcessingPorts { get; set; }

        /// <summary>
        /// Configures a specific input port on a downstream component. This method will perform a shallow copy of the output
        /// port it is to be connected to.
        /// </summary>
        /// <param name="encodingType">The encoding type the input port shall be expecting.</param>
        /// <param name="pixelFormat">The pixel format the input port shall be expecting</param>
        /// <param name="copyPort">The output port we are copying format data from</param>
        public virtual unsafe void ConfigureInputPort(MMALEncoding encodingType, MMALEncoding pixelFormat, MMALPortImpl copyPort)
        {
            this.InitialiseInputPort(0);

            if (copyPort != null)
            {
                copyPort.ShallowCopy(this.Inputs[0]);
            }

            if (encodingType != null)
            {
                this.Inputs[0].Ptr->Format->encoding = encodingType.EncodingVal;
            }

            if (pixelFormat != null)
            {
                this.Inputs[0].Ptr->Format->encodingVariant = pixelFormat.EncodingVal;
            }

            this.Inputs[0].Commit();
        }

        /// <summary>
        /// Call to configure changes on an Image Encoder input port. Used when providing an image file directly
        /// to the component.
        /// </summary>
        /// <param name="encodingType">The encoding type the input port will expect data in</param>
        /// <param name="pixelFormat">The pixel format the input port will expect data in</param>
        /// <param name="width">The width of the incoming frame</param>
        /// <param name="height">The height of the incoming frame</param>
        public virtual unsafe void ConfigureInputPort(MMALEncoding encodingType, MMALEncoding pixelFormat, int width, int height)
        {
            this.InitialiseInputPort(0);

            if (encodingType != null)
            {
                this.Inputs[0].Ptr->Format->encoding = encodingType.EncodingVal;
            }

            if (pixelFormat != null)
            {
                this.Inputs[0].Ptr->Format->encodingVariant = pixelFormat.EncodingVal;
            }

            this.Inputs[0].Ptr->BufferNum = this.Inputs[0].Ptr->BufferNumMin;
            this.Inputs[0].Ptr->BufferSize = this.Inputs[0].Ptr->BufferSizeMin;

            this.Inputs[0].EncodingType = encodingType;

            this.Inputs[0].Commit();

            if (this.Outputs[0].Ptr->Format->type == MMALFormat.MMAL_ES_TYPE_T.MMAL_ES_TYPE_UNKNOWN)
            {
                throw new PiCameraError("Unable to determine settings for output port.");
            }
        }

        /// <summary>
        /// Call to configure changes on the 1st Downstream component output port.
        /// </summary>
        /// <param name="encodingType">The encoding type this output port will send data in</param>
        /// <param name="pixelFormat">The pixel format this output port will send data in</param>
        /// <param name="quality">The quality of our outputted data</param>
        /// <param name="bitrate">The bitrate we are sending data at</param>
        public virtual unsafe void ConfigureOutputPort(MMALEncoding encodingType, MMALEncoding pixelFormat, int quality, int bitrate = 0)
        {
            this.ConfigureOutputPort(0, encodingType, pixelFormat, quality, bitrate);
        }

        /// <summary>
        /// Call to configure changes on an Downstream component output port.
        /// </summary>
        /// <param name="outputPort">The output port we are configuring</param>
        /// <param name="encodingType">The encoding type this output port will send data in</param>
        /// <param name="pixelFormat">The pixel format this output port will send data in</param>
        /// <param name="quality">The quality of our outputted data</param>
        /// <param name="bitrate">The bitrate we are sending data at</param>
        public virtual unsafe void ConfigureOutputPort(int outputPort, MMALEncoding encodingType, MMALEncoding pixelFormat, int quality, int bitrate = 0)
        {
            this.InitialiseOutputPort(outputPort);
            this.ProcessingPorts.Add(outputPort);

            this.Inputs[0].ShallowCopy(this.Outputs[outputPort]);

            if (encodingType != null)
            {
                this.Outputs[outputPort].Ptr->Format->encoding = encodingType.EncodingVal;
            }

            if (pixelFormat != null)
            {
                this.Outputs[outputPort].Ptr->Format->encodingVariant = pixelFormat.EncodingVal;
            }

            MMAL_VIDEO_FORMAT_T tempVid = this.Outputs[outputPort].Ptr->Format->es->video;

            try
            {
                this.Outputs[outputPort].Commit();
            }
            catch
            {
                // If commit fails using new settings, attempt to reset using old temp MMAL_VIDEO_FORMAT_T.
                MMALLog.Logger.Warn("Commit of output port failed. Attempting to reset values.");
                this.Outputs[outputPort].Ptr->Format->es->video = tempVid;
                this.Outputs[outputPort].Commit();
            }

            if (encodingType == MMALEncoding.JPEG)
            {
                this.Outputs[outputPort].SetParameter(MMALParametersCamera.MMAL_PARAMETER_JPEG_Q_FACTOR, quality);
            }

            this.Outputs[outputPort].EncodingType = encodingType;
            this.Outputs[outputPort].PixelFormat = pixelFormat;

            this.Outputs[outputPort].Ptr->Format->es->video.height = MMALUtil.VCOS_ALIGN_UP(this.Height, 32);
            this.Outputs[outputPort].Ptr->Format->es->video.width = MMALUtil.VCOS_ALIGN_UP(this.Width, 32);
            this.Outputs[outputPort].Ptr->Format->es->video.crop = new MMAL_RECT_T(0, 0, this.Width, this.Height);

            this.Outputs[outputPort].Ptr->BufferNum = Math.Max(this.Outputs[outputPort].Ptr->BufferNumRecommended, this.Outputs[outputPort].Ptr->BufferNumMin);
            this.Outputs[outputPort].Ptr->BufferSize = Math.Max(this.Outputs[outputPort].Ptr->BufferSizeRecommended, this.Outputs[outputPort].Ptr->BufferSizeMin);

            // It is important to re-commit changes to width and height.
            this.Outputs[outputPort].Commit();
        }

        public override void Dispose()
        {
            MMALLog.Logger.Debug("Removing downstream component");

            this.ClosePipelineConnections();

            // Remove any unmanaged resources held by the capture handler.
            this.Handler?.Dispose();

            MMALCamera.Instance.DownstreamComponents.Remove(this);

            base.Dispose();
        }

        /// <summary>
        /// Initialises the input port specified by constructing the correct port type to be used by the component.
        /// </summary>
        /// <param name="inputPort">The input port to initialise</param>
        internal virtual unsafe void InitialiseInputPort(int inputPort)
        {
        }

        /// <summary>
        /// Initialises the output port specified by constructing the correct port type to be used by the component.
        /// </summary>
        /// <param name="outputPort">The output port to initialise</param>
        internal virtual unsafe void InitialiseOutputPort(int outputPort)
        {
        }

        /// <summary>
        /// Responsible for closing and destroying any connections associated with this component prior to disposing.
        /// </summary>
        private void ClosePipelineConnections()
        {
            // Close any connection held by this component
            foreach (var input in this.Inputs)
            {
                if (input.ConnectedReference != null)
                {
                    MMALLog.Logger.Debug($"Removing {input.ConnectedReference.ToString()}");

                    input.ConnectedReference.OutputPort.ConnectedReference?.Dispose();
                    input.ConnectedReference.OutputPort.ConnectedReference = null;

                    input.ConnectedReference.Dispose();
                    input.ConnectedReference = null;
                }
            }

            foreach (var output in this.Outputs)
            {
                if (output.ConnectedReference != null)
                {
                    MMALLog.Logger.Debug($"Removing {output.ConnectedReference.ToString()}");

                    output.ConnectedReference.Dispose();
                    output.ConnectedReference = null;
                }
            }
        }
    }
}
