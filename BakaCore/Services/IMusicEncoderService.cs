using System;
using System.IO;
using System.Threading.Tasks;

namespace BakaCore.Services
{
	public interface IMusicEncoderService
	{
		Task<Stream> EncodeAsOggOpusAsync(Stream audioStream);
	}
}
