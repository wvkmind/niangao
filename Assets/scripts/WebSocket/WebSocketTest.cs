using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using System.Text;
using LitJson;
using System.Net.WebSockets;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using UnityEditor;
using UnityEngine.UI;

public class WebSocketTest : MonoBehaviour
{
	public Image image; 
	public Text testText;
    public Text testText2;

	private List<String> messageList = new List<String>();

    public int roomID;

	public bool isTextInput;
	public bool IsLink=true;

	ClientWebSocket ws = new ClientWebSocket();
	CancellationToken ct = new CancellationToken();
	WebSocketState webSocketState = new WebSocketState();

	List<JsonData> danmuJsonList = new List<JsonData>();



	private void Awake()
	{
		
	}

	Thread receiveThread2;

	// Start is called before the first frame update
	void Start()
    {
		roomID = 11053010;
		WebSocketBiliBiliMsg();
	}

    // Update is called once per frame
    void Update()
    {

	}


    public async void WebSocketBiliBiliMsg()
	{
		var room_id = roomID;
		JsonData jsonData = new JsonData();
		jsonData["uid"] = 0;
		jsonData["roomid"] = room_id;
		jsonData["protover"] = 3;
		jsonData["platform"] = "web";
		jsonData["clientver"] = "1.6.3";
		jsonData["type"] = 2;
		jsonData["key"] = "token";
		string json1 = jsonData.ToJson();

		//Debug.Log(json1);

		MsgBody sBody = new MsgBody(json1);
		sBody.PacketLength = Encoding.UTF8.GetBytes(json1).Length + 16;
		sBody.HeaderLength = 16;
		sBody.ProtocolVersion = 1;
		sBody.Operation = 7;
		sBody.SequenceId = 1;

		//Debug.Log(sBody.ToByteArray()[5]);

		try
		{
			// add header
			//ws.Options.SetRequestHeader("X-Token", "eyJhbGciOiJIUzI1N");

			Uri url = new Uri("wss://broadcastlv.chat.bilibili.com:2245/sub");
			await ws.ConnectAsync(url, ct);
			await ws.SendAsync(new ArraySegment<byte>(sBody.ToByteArray()), WebSocketMessageType.Binary, true, ct);
            testText.text = "连上服务器";
			//testText.text = "请求完成";
		}
		catch (Exception ex)
		{
            testText.text = ex.Message;
			if (ex.Message == "The remote party closed the WebSocket connection without completing the close handshake.")
			{
                testText.text = "服务器关闭";
				//testText.text = "服务器关闭";
			}
		}

		//通过ClientWebSocket接收数据 

		reMsg(ws);

		//Thread receiveThread = new Thread(reMsg);
		//receiveThread.Start(ws);

		receiveThread2 = new Thread(SendMsg);

		receiveThread2.Start(ws);

	}

	async void reMsg(object ws)
	{
		ClientWebSocket sk = (ClientWebSocket)ws;
		byte[] saveArr = new byte[0];
		var result = new byte[4096];

		int PacketLength1 = 0;//封包总大小
		byte[] PacketLengthByte1;

		Int16 HeaderLength1 = 16;//头部长度
		byte[] HeaderLengthByte1;

		Int16 ProtocolVersion1 = 1;//协议版本
		byte[] ProtocolVersionByte1;

		int Operation1 = 7;//操作码 7表示认证并加入房间
		byte[] OperationByte1;

		int SequenceId1 = 1;//就1
		byte[] SequenceIdByte1;

		string BodyData1;//包体数据
		byte[] BodyDataByte1;

		while (IsLink)
		{
            try
			{			
				await sk.ReceiveAsync(new ArraySegment<byte>(result), new CancellationToken());
				//Debug.Log(sk.State);
				PacketLengthByte1 = SubByte(result, 0, 4);
				//Debug.Log("result____" + result.Length);
				Array.Reverse(PacketLengthByte1);
				PacketLength1 = BitConverter.ToInt32(PacketLengthByte1, 0);
				//Debug.Log("PacketLength1_____"+PacketLength1);

				HeaderLengthByte1 = SubByte(result, 4, 2);
				Array.Reverse(HeaderLengthByte1);
				HeaderLength1= BitConverter.ToInt16(HeaderLengthByte1, 0);
				//Debug.Log("HeaderLength1_____" + HeaderLength1);

				ProtocolVersionByte1 = SubByte(result, 6, 2);
				Array.Reverse(ProtocolVersionByte1);
				ProtocolVersion1 = BitConverter.ToInt16(ProtocolVersionByte1, 0);
				//Debug.Log("ProtocolVersion1_____" + ProtocolVersion1);

				OperationByte1 = SubByte(result, 8, 4);
				Array.Reverse(OperationByte1);
				Operation1 = BitConverter.ToInt16(OperationByte1, 0);
				//Debug.Log("Operation1_____" + Operation1);
				SequenceIdByte1 = SubByte(result, 12, 4);
				Array.Reverse(SequenceIdByte1);
				SequenceId1 = BitConverter.ToInt16(SequenceIdByte1, 0);
				//Debug.Log("SequenceId1_____" + SequenceId1);

				if(Operation1==5)//弹幕
				{
					int offset = 0;
					string BodyStr="";
					while (offset< PacketLength1)
					{
						BodyData1 = "";
						BodyDataByte1 = SubByte(result, offset+16, offset+PacketLength1);
						try
						{
							// pako可能无法解压
							//Array.Reverse(BodyDataByte1);
							if (ProtocolVersion1 == 3)
							{
								byte[] BodyDataByte2 = BufferDecompress(BodyDataByte1);
								BodyData1 = Encoding.UTF8.GetString(BodyDataByte2, 0, BodyDataByte2.Length);

								//AssetDatabase.Refresh();
								//Debug.Log("BodyData1Lenght{" + BodyData1[1] + "}");
								danmuJsonList=DtrSplitToJson(BodyData1);
								foreach (var msgItem in danmuJsonList)
								{
									//print(msgItem["cmd"]);
									
									if ((string)msgItem["cmd"] == "DANMU_MSG")
									{
										if (isTextInput)
										{
											string path = Application.dataPath + "/testMsg.txt";
											using (FileStream fs = new FileStream(path, FileMode.Create))
											{
												fs.Write(BodyDataByte2, 0, BodyDataByte2.Length);
												fs.Dispose();
											}
										}
										//testText.text = msgItem["info"][2][1].ToString()+":"+ msgItem["info"][1].ToString();
										
										if (msgItem["info"][1].ToString().Equals("随机点歌")) {
											String[] songList = { "I Love You",
"letting go",
"six feet under",
"微",
"花",
"囹圄",
"落海",
"下落",
"下潜",
"海风",
"房间",
"撒野",
"三月",
"赤伶",
"游京",
"告石",
"大鱼",
"画皮",
"像鱼",
"蔓延",
"瑕疵",
"呓语",
"儿歌",
"昔言",
"谪仙",
"海底",
"指纹",
"保留",
"小宇",
"故梦",
"百利甜",
"难生恨",
"两点半",
"孽海记",
"蝶恋花",
"静悄悄",
"七里香",
"莫妮卡",
"罗刹鸟",
"伪善者",
"童话镇",
"喜欢你",
"肯定否",
"错位时空",
"爱人错过",
"等等等等",
"那么骄傲",
"易安难安",
"气象站台",
"往后余生",
"漠河舞厅",
"狂热忏悔",
"人间指南",
"夏天的风",
"星空剪影",
"命有波澜",
"再见深海",
"我是透明",
"深海幻境",
"从未背离",
"乌梅子酱",
"第三人称",
"等雾散去",
"遗憾也值得",
"在夜里跳舞",
"晚来天欲雪",
"所念皆星河",
"丹青倦华容",
"把爱情当真",
"被驯服的象",
"别找我麻烦",
"江湖撰事录",
"一直很安静",
"白马入芦花",
"我好喜欢你",
"阿拉斯加海湾",
"化身孤岛的鲸",
"热爱105℃的你",
"推开世界的门",
"人类饲养指南",
"一身诗意千寻瀑",
"想找个人来爱我",
"陪你度过漫长岁月",
"我的一个道姑朋友",
"夜是克莱因蓝的忧伤",
"曾经我也想一了百了",
"就让这大雨全都落下",
"燕子没有你我要怎么活",
"遇见你的时候所有星星都落到我头上" };
                                            
                                            System.Random rd = new System.Random();
                                            messageList.Add(msgItem["info"][2][1].ToString() + "随机点歌:" + songList[rd.Next(0, 88)]);
											if (messageList.Count >= 15) {
                                                messageList.RemoveAt(0);
                                            }
											String curString = "";
											for (int i = 0; i < messageList.Count; i++) {
												if (i == 0){
                                                    curString = messageList[i];
                                                }
												else {
                                                    curString = curString + "\n" + messageList[i];
                                                }
												
                                            }
                                            testText.text = curString;

                                        }
										
									}
									if ((string)msgItem["cmd"] == "SEND_GIFT")
                                    {
                                        
                                    }
								}
							}
						}
						catch (Exception ex)
						{
                            testText.text = ex.Message;
							//stText.text = ex.Message;
							BodyData1 = Encoding.UTF8.GetString(BodyDataByte1, 0, BodyDataByte1.Length);
						}
						BodyStr = BodyData1;
						offset += PacketLength1;
					}
				}
			}
			catch (Exception ex)
			{
                testText.text = ex.Message;
				if (ex.Message == "The remote party closed the WebSocket connection without completing the close handshake.")
				{
                    testText.text = "服务器关闭";
				}
			}
		}
	}
	/// <summary>
	/// 心跳
	/// </summary>
	/// <param name="socket"></param>
	public async void SendMsg(object ws)
	{
		MsgBody sBody1 = new MsgBody("");
		sBody1.PacketLength = Encoding.UTF8.GetBytes("").Length + 16;
		sBody1.HeaderLength = 16;
		sBody1.ProtocolVersion = 1;
		sBody1.Operation = 2;
		sBody1.SequenceId = 1;
		ClientWebSocket sk = (ClientWebSocket)ws;
		while (true)
		{
			await sk.SendAsync(new ArraySegment<byte>(sBody1.ToByteArray()), WebSocketMessageType.Binary, true, ct);
            Thread.Sleep(30000);
			if (!IsLink)
				break;
		}
        if (receiveThread2.ThreadState == ThreadState.Running)
        {
            print("接收线程关闭！");
            receiveThread2.Abort();
        }

    }
	private void OnDestroy()
	{
        Application.Quit();

        receiveThread2.Abort();
        //关闭通讯线程
        IsLink = false;
	}
	/// <summary>  
	/// 截取字节数组  
	/// </summary>  
	/// <param name="srcBytes">要截取的字节数组</param>  
	/// <param name="startIndex">开始截取位置的索引</param>  
	/// <param name="length">要截取的字节长度</param>  
	/// <returns>截取后的字节数组</returns>  
	public static byte[] SubByte(byte[] srcBytes, int startIndex, int length)
	{
		System.IO.MemoryStream bufferStream = new System.IO.MemoryStream();
		byte[] returnByte = new byte[] { };
		if (srcBytes == null) { return returnByte; }
		if (startIndex < 0) { startIndex = 0; }
		if (startIndex < srcBytes.Length)
		{
			if (length < 1 || length > srcBytes.Length - startIndex) { length = srcBytes.Length - startIndex; }
			bufferStream.Write(srcBytes, startIndex, length);
			returnByte = bufferStream.ToArray();
			bufferStream.SetLength(0);
			bufferStream.Position = 0;
		}
		bufferStream.Close();
		bufferStream.Dispose();
		return returnByte;
	}
	public int readInt(byte[] buffer, int start, int lengh)
	{
		int result = 0;
		for(int i = lengh - 1;i>= 0;i--)
		{
			result += (int)Math.Pow(256, lengh - i - 1 * buffer[start + i]);
		}
		return result;
	}
    // 使用System.IO.Compression进行Deflate解压
    private byte[] Decompress(byte[] data, bool byByte)
    {
        byte[] buffer = new byte[65536];
        System.IO.MemoryStream input = new System.IO.MemoryStream(data);
        System.IO.MemoryStream output = new System.IO.MemoryStream();
        Org.Brotli.Dec.BrotliInputStream brotliInput = new Org.Brotli.Dec.BrotliInputStream(input);
        if (byByte)
        {
            byte[] oneByte = new byte[1];
            while (true)
            {
                int next = brotliInput.ReadByte();
                if (next == -1)
                {
                    break;
                }
                oneByte[0] = unchecked((byte)next);
                output.Write(oneByte, 0, 1);
            }
        }
        else
        {
            while (true)
            {
                int len = brotliInput.Read(buffer, 0, buffer.Length);
                if (len <= 0)
                {
                    break;
                }
                output.Write(buffer, 0, len);
            }
        }
        brotliInput.Close();
        return output.ToArray();
    }
    public byte[] BufferDecompress(byte[] data)
	{
		//testText.text = "正在解压缩";
		Debug.Log("正在解压缩");
		byte[] result = Decompress(data,true);
		return result;
	}
	public static int bytesToInt(byte[] src, int offset)
	{
		int value;
		value = (int)((src[offset] & 0xFF)
				| ((src[offset + 1] & 0xFF) << 8)
				| ((src[offset + 2] & 0xFF) << 16)
				| ((src[offset + 3] & 0xFF) << 24));
		return value;
	}


	/** 
	* byte数组中取int数值，本方法适用于(低位在后，高位在前)的顺序。和intToBytes2（）配套使用
	*/
	public static int bytesToInt2(byte[] src, int offset)
	{
		int value;
		value = (int)(((src[offset] & 0xFF) << 24)
				| ((src[offset + 1] & 0xFF) << 16)
				| ((src[offset + 2] & 0xFF) << 8)
				| (src[offset + 3] & 0xFF));
		return value;
	}
	public List<JsonData> DtrSplitToJson(string str)
	{
		string item = str;
		List<JsonData> msgJsonStr = new List<JsonData>();
		//print(item.Length);
		int count = -1;
		int Startcharindex = -1;
		int Endcharindex = -1;
		for (int i = 0; i < item.Length; i++)
		{
			if (item[i] == '{')
			{
				if (Startcharindex == -1)
				{
					count = 0;
					Startcharindex = i;
				}
				count++;
			}
			if (item[i] == '}')
			{
				count--;
			}
			if (count == 0)
			{
				Endcharindex = i;
				int lengh = Endcharindex - Startcharindex + 1;
				//print(item.Substring(Startcharindex, lengh));
				JsonData msgJsonData = JsonMapper.ToObject(item.Substring(Startcharindex, lengh));
				msgJsonStr.Add(msgJsonData);
				count = -1;
				Startcharindex = -1;
				Endcharindex = -1;
			}

		}
		return msgJsonStr;
	}
}



