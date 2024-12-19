using Magic_RDR.Application;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using static Magic_RDR.RPF6.RPF6TOC;

namespace Magic_RDR.Viewers
{
    public partial class AudioViewerForm : Form
    {
        private string awcPath;
        private string[] wavPath;
        private int SampleCount;
        private List<int> AudioIds = new List<int>();
        private bool IsSwitch { get; set; }

        public AudioViewerForm(TOCSuperEntry entry)
        {
            InitializeComponent();
            this.Text = string.Format("MagicRDR - Audio Player [{0}]", entry.Entry.Name);

            this.IsSwitch = AppGlobals.Platform == AppGlobals.PlatformEnum.Switch;
            FileEntry file = entry.Entry.AsFile;
            RPFFile.RPFIO.Position = file.GetOffset();

            byte[] buffer = RPFFile.RPFIO.ReadBytes(file.SizeInArchive);
            IOReader reader = new IOReader(new MemoryStream(buffer), this.IsSwitch ? IOReader.Endian.Little : IOReader.Endian.Big);

            if (this.IsSwitch)
            {
                wavPath = AWC_ReadData(reader, file.Name);
                if (wavPath == null || wavPath.Length == 0)
                    return;

                List<ListViewItem> listItem = wavPath.Select(s => new ListViewItem(Path.GetFileName(s).Replace(".wav", ""))).ToList();
                columnHeaderAudio.Text = string.Format("Audio Files ({0})", wavPath.Length);
                listView.BeginUpdate();
                listView.Items.Clear();
                listView.Items.AddRange(listItem.ToArray());
                listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
                listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
                listView.EndUpdate();

                audioPlayer.URL = wavPath[0].Substring(0, wavPath[0].IndexOf(" "));
                audioPlayer.Ctlcontrols.play();
                return;
            }

            if (!File.Exists(System.Windows.Forms.Application.StartupPath + "\\Assemblies\\" + "libutils.exe"))
            {
                MessageBox.Show("Required file 'libutils.exe' is missing", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            byte[] bytes = new byte[4];
            Array.Copy(buffer, 0x8, bytes, 0, 4);

            SampleCount = BitConverter.ToInt32(bytes, 0).Swap();
            string assemblyPath = System.Windows.Forms.Application.StartupPath + "\\Assemblies\\";
            awcPath = assemblyPath + entry.Entry.Name;

            File.WriteAllBytes(awcPath, buffer);
            if (!File.Exists(awcPath))
            {
                MessageBox.Show("An error occured while decoding audio", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            List<ListViewItem> list = new List<ListViewItem>();
            wavPath = new string[SampleCount];
            for (int i = 0; i < SampleCount; i++)
            {
                wavPath[i] = string.Format("{0}_{1}.wav", awcPath.Substring(0, awcPath.LastIndexOf(".")), i + 1);

                Process process = new Process();
                process.StartInfo.FileName = assemblyPath + "libutils.exe";
                process.StartInfo.Arguments = string.Format("-s {0} -o \"{1}\" \"{2}\"", i + 1, wavPath[i], awcPath);
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.UseShellExecute = true;
                process.Start();

                while (!process.HasExited)
                    Thread.Sleep(500);

                ListViewItem item = new ListViewItem();
                item.Text = Path.GetFileNameWithoutExtension(awcPath) + "_" + (i + 1).ToString();
                list.Add(item);
            }
            columnHeaderAudio.Text = string.Format("Audio Files ({0})", SampleCount);
            listView.BeginUpdate();
            listView.Items.Clear();
            listView.Items.AddRange(list.ToArray());
            listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
            listView.EndUpdate();

            Thread.Sleep(1000);
            if (!File.Exists(wavPath[0]))
            {
                MessageBox.Show("An error occured while decoding audio", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }
        }

        #region Controls

        private void AudioViewerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!string.IsNullOrEmpty(awcPath))
            {
                if (File.Exists(awcPath))
                {
                    try { File.Delete(awcPath); }
                    catch { }
                }
            }

            if (wavPath == null)
                return;

            foreach (string path in wavPath)
            {
                if (File.Exists(path))
                {
                    try { File.Delete(path); }
                    catch { }
                }
            }
        }

        private void listView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count <= 0)
                return;

            string wavFileName = listView.SelectedItems[0].Text;
			wavFileName = wavFileName.Substring(0, wavFileName.IndexOf(' '));

			foreach (string path in wavPath)
            {
                if (path.Contains(wavFileName))
				{
					wavFileName = path.Substring(0, path.IndexOf(' '));
					break;
				}
            }

            if (!File.Exists(wavFileName))
            {
                return;
            }

            audioPlayer.URL = wavFileName;
            audioPlayer.Ctlcontrols.play();
        }

        private void exportButton_Click(object sender, EventArgs e)
        {
            if (wavPath.Length <= 0)
            {
                MessageBox.Show("There's nothing to export", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string currentAudioFile;
            if (listView.SelectedItems.Count <= 0)
                currentAudioFile = wavPath[0];
            else
                currentAudioFile = listView.SelectedItems[0].Text;

			currentAudioFile = currentAudioFile.Substring(0, currentAudioFile.IndexOf(' '));

			foreach (string file in wavPath)
            {
                if (file.Contains(currentAudioFile))
				{
                    currentAudioFile = file.Substring(0, file.IndexOf(' '));
					break;
				}
            }

            if (!File.Exists(currentAudioFile))
            {
                MessageBox.Show("An error occured while exporting", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

			SaveFileDialog dialog = new SaveFileDialog
			{
				Filter = "WAV Audio File (*.wav)|*.wav",
				FileName = Path.GetFileNameWithoutExtension(currentAudioFile)
			};

			if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (File.Exists(dialog.FileName))
                {
                    File.Delete(dialog.FileName);
                }
                File.Copy(currentAudioFile, dialog.FileName);
                MessageBox.Show("Successfully exported WAV !", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void exportAllButton_Click(object sender, EventArgs e)
        {
            if (wavPath.Length <= 0)
            {
                MessageBox.Show("There's nothing to export", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

			FolderBrowserDialog dialog = new FolderBrowserDialog
			{
				Description = "Select a destination to export audio files",
				ShowNewFolderButton = true
			};

			if (dialog.ShowDialog() == DialogResult.OK)
            {
                int count = 0;
                foreach (string file in wavPath)
                {
					var path = file.Substring(0, file.IndexOf(' '));
					if (File.Exists(path))
                    {
                        File.Copy(path, dialog.SelectedPath + "\\\\" + Path.GetFileName(path));
                        count++;
                    }
                }
                MessageBox.Show(string.Format("Successfully export {0} file(s) !", count), "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        #endregion

        private new AwcChunkType Tag(string s)
        {
            return Tag(DataUtils.GetHash(s));
        }

        private new AwcChunkType Tag(uint hash)
        {
            return (AwcChunkType)(hash & 0xff);
        }

        private string[] AWC_ReadData(IOReader reader, string entryName)
        {
            uint magic = reader.ReadUInt32(); //TADA
            uint versionAndFlags = reader.ReadUInt32(); //4278255617
            short version = (short)(versionAndFlags & 0xFFFF); //1
            ushort flags = (ushort)((versionAndFlags >> 16) & 0xFFFF); //65281
            int streamsCount = (int)reader.ReadUInt32(); //16
            int streamsInfoOffset = (int)reader.ReadUInt32(); //496

            if ((flags >> 8) != 0xFF)
            {
                throw new Exception("Unsupported flag");
            }

            //First bit - means that there are unknown word for each stream after this header
            //Second bit - I think that it means that not all the tags are in the start of the file, but all the tags of a stream are near the data tag
            //Third bit - Multi channel audio
            if ((flags & 0xF8) != 0)
            {
                throw new Exception("Unsupported flag");
            }

            bool singleChannelEncryptFlag = (flags & 2) == 2;
            bool multiChannel = (flags & 4) == 4;
            bool multiChannelEncryptFlag = (flags & 8) == 8;

            reader.BaseStream.Seek(this.IsSwitch ? 0x10 : 0x10 + ((flags & 1) == 1 ? (2 * streamsCount) : 0), SeekOrigin.Begin);
            long offset = reader.BaseStream.Position;

            if (this.IsSwitch)
            {
                if ((flags & 0x00010000) == 0)
                {
                    offset += 0x2 * streamsCount;
                }

				reader.BaseStream.Position = offset;
				uint layer = reader.ReadUInt32();
                bool isMusic = (layer & 0x1FFFFFFF) == 0x0;

                reader.BaseStream.Position = offset;
			}

            List<StreamInfo> info = new List<StreamInfo>();
            List<string> wavPaths = new List<string>();
            Dictionary<int, Dictionary<AwcChunkType, ChunkInfo>> streamsChunks = new Dictionary<int, Dictionary<AwcChunkType, ChunkInfo>>();

            for (int i = 0; i < streamsCount; ++i)
            {
                info.Add(new StreamInfo(reader));
            }

            for (int i = 0; i < streamsCount; ++i)
            {
                streamsChunks[info[i].Id] = new Dictionary<AwcChunkType, ChunkInfo>();
                for (int j = 0; j < info[i].TagsCount; ++j)
                {
                    ChunkInfo chunk = new ChunkInfo(reader);
                    streamsChunks[info[i].Id][chunk.Tag] = chunk;
                }
            }

            if (!multiChannel)
            {
                var chunkList = new List<FormatChunk>();
                var chunkFormatList = new List<ChannelsInfoChunkHeader>();

				for (int i = 0; i < streamsCount; i++)
				{
					if (streamsChunks.TryGetValue(info[i].Id, out Dictionary<AwcChunkType, ChunkInfo> codecInfo))
					{
						codecInfo.TryGetValue(Tag("data"), out ChunkInfo chunkDataInfo);
						codecInfo.TryGetValue(Tag("streamformat"), out ChunkInfo chunkStreamFormatInfo);
						codecInfo.TryGetValue(Tag("format"), out ChunkInfo chunkFormatInfo);

						//Format
						FormatChunk chunk = null;
						if (chunkFormatInfo != null)
						{
							reader.BaseStream.Position = chunkFormatInfo.Offset;
							chunk = new FormatChunk(reader);
							chunkList.Add(chunk);
						}

						//StreamFormat
						ChannelsInfoChunkHeader chunkFormat = null;
						if (chunkStreamFormatInfo != null)
						{
							reader.BaseStream.Position = chunkStreamFormatInfo.Offset;
							chunkFormat = new ChannelsInfoChunkHeader(reader);
							chunkFormatList.Add(chunkFormat);
						}

						//Data
						if (chunkDataInfo != null && (chunk != null || chunkFormat != null))
						{
							reader.BaseStream.Position = chunkDataInfo.Offset;
							byte[] data = new byte[chunkDataInfo.Size];
							if (chunkDataInfo.Tag == AwcChunkType.data)
							{
								if (chunk.Codec == AwcCodecType.OPUS)
								{		
									
								}
								else if (chunk.Codec == AwcCodecType.MSADPCM)
								{
									var header = reader.ReadBytes(0x4A);
									var brHeader = new BinaryReader(new MemoryStream(header));

									brHeader.BaseStream.Position = 0x18;
									var samplesHeader = brHeader.ReadInt32();

									brHeader.BaseStream.Position = 0x20;
									var blockSize = brHeader.ReadInt16();

									var audioSize = reader.ReadInt32();
									data = reader.ReadBytes(audioSize);

									int waveSize = (int)(reader.BaseStream.Position - chunkDataInfo.Offset);
									reader.BaseStream.Position = chunkDataInfo.Offset;
									var waveData = reader.ReadBytes(waveSize);

									int samplesToDo = (blockSize - 7) * 2 + 2;
									int samples_filled = 0;
									var decodedAudio = new List<byte>();

									while (samples_filled < chunk.Samples)
									{
										var chunkData = DecodeMSADPCMMono(data, 1, samples_filled, samplesToDo, 0, blockSize);
										decodedAudio.AddRange(chunkData);
										samples_filled += samplesToDo;
									}

									string hexString = info[i].Id.ToString("X");
									string desiredFileName = string.Format("0x{0}.wav", hexString.Length == 7 ? ("0" + hexString) : hexString);
									if (desiredFileName == "0x0.wav")
									{
										desiredFileName = entryName.Replace(".awc", ".wav");
									}

									string tempFile = Path.Combine(Path.GetTempPath(), desiredFileName);
									using (var fs = new FileStream(tempFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
									{
										if (chunk != null)
											WAVFromPCM(new MemoryStream(decodedAudio.ToArray()), fs, 1, samplesHeader, 16);
										else
											WAVFromPCM(new MemoryStream(decodedAudio.ToArray()), fs, 1, samplesHeader, 16);

									}
									wavPaths.Add(tempFile + " - " + samplesHeader + " Hz");
								}
							}
						}
					}
				}
            }
            return wavPaths.ToArray();
        }

		private static readonly short[,] msadpcm_coefs =
		{
			{ 256, 0 },
			{ 512, -256 },
			{ 0, 0 },
			{ 192, 64 },
			{ 240, 0 },
			{ 460, -208 },
			{ 392, -232 }
		};

		/* AdaptionTable */
		private static readonly short[] msadpcm_steps =
		{
			230, 230, 230, 230,
			307, 409, 512, 614,
			768, 614, 512, 409,
			307, 230, 230, 230
		};

		public short[] adpcmCoef = new short[16];
		public short adpcmHistory1_16 = 0;
		public short adpcmHistory2_16 = 0;
		public int adpcmScale = 0;

		public int Clamp16(int val)
		{
			if (val > 32767) return 32767;
			else if (val < -32768) return -32768;
			else return val;
		}

		private void WriteShortToByteArray(byte[] buffer, int index, short value)
		{
			buffer[index] = (byte)(value & 0xFF);
			buffer[index + 1] = (byte)((value >> 8) & 0xFF);
		}

		public byte[] DecodeMSADPCMMono(byte[] data, int channelSpacing, int firstSample, int samplesToDo, int channel, int bytesPerFrame)
		{
			var br = new BinaryReader(new MemoryStream(data));
			int framesIn;
			int samplesPerFrame = (bytesPerFrame - 7) * 2 + 2;
			bool isShr = true;

			framesIn = firstSample / samplesPerFrame;
			br.BaseStream.Position = framesIn * bytesPerFrame;
			firstSample %= samplesPerFrame;

			int index = framesIn * bytesPerFrame;
			if (index >= data.Length)
			{
				index = (framesIn - 1) * bytesPerFrame;
			}

			if (firstSample == 0)
			{
				int coefIndex = data[index] & 0x07;
				adpcmCoef[0] = msadpcm_coefs[coefIndex, 0];
				adpcmCoef[1] = msadpcm_coefs[coefIndex, 1];
				adpcmScale = BitConverter.ToInt16(data, index + 1);
				adpcmHistory1_16 = BitConverter.ToInt16(data, index + 3);
				adpcmHistory2_16 = BitConverter.ToInt16(data, index + 5);
			}

			byte[] outBuffer = new byte[samplesToDo * 2 * channelSpacing];
			int outIndex = 0;

			if (firstSample == 0 && samplesToDo > 0)
			{
				WriteShortToByteArray(outBuffer, outIndex, adpcmHistory2_16);
				outIndex += channelSpacing * 2;
				firstSample++;
				samplesToDo--;
			}

			if (firstSample == 1 && samplesToDo > 0)
			{
				WriteShortToByteArray(outBuffer, outIndex, adpcmHistory1_16);
				outIndex += channelSpacing * 2;
				firstSample++;
				samplesToDo--;
			}

			for (int i = firstSample; i < firstSample + samplesToDo; i++)
			{
				byte currentByte = data[index + 7 + (i - 2) / 2];
				int shift = (i % 2 == 0) ? 4 : 0;
				short decodedSample = isShr ? MSADPCMExpandNibbleShr(currentByte, shift) : MSADPCMExpandNibbleDiv(currentByte, shift);

				WriteShortToByteArray(outBuffer, outIndex, decodedSample);
				outIndex += channelSpacing * 2;
			}
			return outBuffer;
		}

		private short MSADPCMExpandNibbleShr(byte currentByte, int shift)
		{
			int code = (currentByte >> shift) & 0x0F;
			if ((code & 0x08) != 0) code -= 16;

			int predicted = adpcmHistory1_16 * adpcmCoef[0] + adpcmHistory2_16 * adpcmCoef[1];
			predicted >>= 8;
			predicted += code * adpcmScale;
			predicted = Clamp16(predicted);

			adpcmHistory2_16 = adpcmHistory1_16;
			adpcmHistory1_16 = (short)predicted;

			adpcmScale = (msadpcm_steps[code & 0x0F] * adpcmScale) >> 8;
			if (adpcmScale < 16) adpcmScale = 16;

			return (short)predicted;
		}

		private short MSADPCMExpandNibbleDiv(byte currentByte, int shift)
		{
			int code = (currentByte >> shift) & 0x0F;
			if ((code & 0x08) != 0) code -= 16;

			int predicted = adpcmHistory1_16 * adpcmCoef[0] + adpcmHistory2_16 * adpcmCoef[1];
			predicted /= 256;
			predicted += code * adpcmScale;
			predicted = Clamp16(predicted);

			adpcmHistory2_16 = adpcmHistory1_16;
			adpcmHistory1_16 = (short)predicted;

			adpcmScale = (msadpcm_steps[code & 0x0F] * adpcmScale) / 256;
			if (adpcmScale < 16) adpcmScale = 16;

			return (short)predicted;
		}

        public void WAVFromPCM(Stream input, Stream output, short channels, int samplesPerSec, int bitsPerSample)
        {
            short sample_size = (short)((bitsPerSample / 8) * channels);
            using (BinaryWriter writer = new BinaryWriter(output))
            {
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write((int)input.Length + 36);
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write((int)16); //Size of header
                writer.Write((short)1); //Format tag - PCM
                writer.Write(channels);
                writer.Write(samplesPerSec);
                writer.Write((int)(sample_size * samplesPerSec)); //average bytes per sec
                writer.Write(sample_size); //full sample size..
                writer.Write((short)bitsPerSample);
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write((int)input.Length); //Skip size of data

				var audioData = new byte[input.Length];
				input.Read(audioData, 0, (int)input.Length);
				writer.Write(audioData);
            }
        }
    }

    public class StreamInfo
    {
        public int TagsCount;
        public int Id;

        public StreamInfo(IOReader reader)
        {
            uint info = reader.ReadUInt32();
            Id = (int)(info & 0x1fffffff);
            TagsCount = (int)(info >> 29);
        }
    }

    public class ChunkInfo
    {
        public AwcChunkType Tag;
        public int Offset;
        public int Size;

        public ChunkInfo(IOReader reader)
        {
            ulong info = reader.ReadUInt64();
            Tag = (AwcChunkType)(info >> 56);
            Offset = (int)(info & 0x0fffffff);
            Size = (int)((info >> 28) & 0x0fffffff);
        }
    }

    public enum AwcChunkType : byte
    {
        //Should be the last byte of the hash of the name.
        data = 0x55,            // 0x5EB5E655
        format = 0xFA,          // 0x6061D4FA
        animation = 0x5C,       // 0x938C925C   not correct
        peak = 0x36,            // 0x8B946236
        mid = 0x68,             // 0x71DE4C68
        gesture = 0x2B,         // 0x23097A2B
        granulargrains = 0x5A,  // 0xE787895A
        granularloops = 0xD9,   // 0x252C20D9
        markers = 0xBD,         // 0xD4CB98BD
        streamformat = 0x48,    // 0x81F95048
        seektable = 0xA3,       // 0x021E86A3
    }

    public enum AwcCodecType
    {
        PCM_16BIT_LITTLE_ENDIAN = 0, //Max Payne 3 PC
        PCM_16BIT_BIG_ENDIAN = 1, //PC & PS3 sfx, rarely
        PCM_32BIT_LITTLE_ENDIAN = 2,
        PCM_32BIT_BIG_ENDIAN = 3,
        ADPCM = 4, //IMA PC
        XMA2 = 5, //Xbox 360
        XWMA = 6, //Xbox 360
        MPEG = 7, //PS3
        VORBIS = 8, //PC only, RDR2
        AAC = 9, //PC only
        WMA = 10, //PC only
        DSP_ADPCM_SFX = 12, //Nintendo Switch
		OPUS = 13,
		ATRAC9 = 15, //PS4
		DSP_ADPCM = 16, //Nintendo Switch
		MSADPCM = 17
    }

    public class FormatChunk
    {
        public uint Samples;
        public int LoopPoint;
        public ushort SamplesPerSecond;
        public ushort Headroom;
        public ushort Loopbegin;
        public ushort Loopend;
        public ushort PlayEnd;
        public byte PlayBegin;
        public AwcCodecType Codec;

        public FormatChunk(IOReader reader)
        {
            Samples = reader.ReadUInt32();
            LoopPoint = reader.ReadInt32();
            SamplesPerSecond = reader.ReadUInt16();
            Headroom = reader.ReadUInt16();
            Loopbegin = reader.ReadUInt16();
            Loopend = reader.ReadUInt16();
            PlayEnd = reader.ReadUInt16();
            PlayBegin = reader.ReadByte();
            Codec = (AwcCodecType)reader.ReadByte();
        }

        public FormatChunk(ChannelsInfoChunkItem channelInfo)
        {
            Samples = channelInfo.Samples;
            LoopPoint = -1;
            SamplesPerSecond = channelInfo.SamplesPerSecond;
            Headroom = channelInfo.Headroom;
            Loopbegin = channelInfo.unknownWord2;
            Loopend = 0;
            PlayEnd = 0;
            PlayBegin = 0;
            Codec = channelInfo.Codec;
        }
    }

    public class ChannelsInfoChunkHeader
    {
        public int BlockCount; //Small number
        public int BigChunkSize;
        public int ChannelsCount;
        public ChannelsInfoChunkItem[] Channels { get; set; }

        public ChannelsInfoChunkHeader(IOReader reader)
        {
            BlockCount = reader.ReadInt32();
            BigChunkSize = reader.ReadInt32();
            ChannelsCount = reader.ReadInt32();

            var channels = new List<ChannelsInfoChunkItem>();
            for (int i = 0; i < ChannelsCount; i++)
            {
                var itemInfo = new ChannelsInfoChunkItem(reader);
                channels.Add(itemInfo);
            }
            Channels = channels.ToArray();
        }
    }

    public class ChannelsInfoChunkItem
    {
        public uint Hash;
        public uint Samples; //Big number
        public ushort Headroom;
        public ushort SamplesPerSecond;
        public AwcCodecType Codec;
        public byte RoundSize;
        public ushort unknownWord2;

        public ChannelsInfoChunkItem(IOReader reader)
        {
			Hash = reader.ReadUInt32();
            Samples = reader.ReadUInt32();
            Headroom = reader.ReadUInt16();
            SamplesPerSecond = reader.ReadUInt16();
            Codec = (AwcCodecType)reader.ReadByte();
            RoundSize = reader.ReadByte();
            unknownWord2 = reader.ReadUInt16();
        }
    }

	public class OpusDecoderWrapper
	{
		[DllImport("Assemblies/opus.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr opus_decoder_create(int Fs, int channels, out int error);

		[DllImport("Assemblies/opus.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int opus_decode(IntPtr decoder, byte[] input, int inputLength, short[] output, int frameSize, int decodeFec);

		[DllImport("Assemblies/opus.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void opus_decoder_destroy(IntPtr decoder);

		public static void DecodeOpus(byte[] opusData, int sampleRate, int channels, int frameSize)
		{
			var decoder = opus_decoder_create(sampleRate, channels, out int error);
			if (error != 0)
			{
				Debug.WriteLine($"Failed to create Opus decoder: {error}");
				return;
			}

			try
			{
				var pcmSamples = new short[frameSize * channels];
				var decodedSamples = opus_decode(decoder, opusData, opusData.Length, pcmSamples, frameSize, 0);

				if (decodedSamples < 0)
				{
					Debug.WriteLine($"Decoding error: {decodedSamples}");
				}
				else
				{
					Debug.WriteLine($"Decoded {decodedSamples} samples.");
				}
			}
			finally
			{
				opus_decoder_destroy(decoder);
			}
		}
	}
}
