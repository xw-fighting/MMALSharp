using System.Collections.Generic;
using MMALSharp.Processors;

namespace MMALSharp.Handlers
{
    public class InMemoryCaptureHandler : CaptureHandlerProcessorBase
    {
        public List<byte> WorkingData { get; set; }

        public InMemoryCaptureHandler()
        {
            this.WorkingData = new List<byte>();
        }
        
        public override void Dispose()
        {
            // Not required.
        }
        
        /// <inheritdoc />
        public override void Process(byte[] data)
        {
            this.WorkingData.AddRange(data);
        }

        /// <inheritdoc />
        public override void PostProcess()
        {
            var tempData = this.WorkingData.ToArray();
            _manipulate(new FrameProcessingContext(tempData, _imageContext));
            this.WorkingData = new List<byte>(tempData);    
        }
    }
}