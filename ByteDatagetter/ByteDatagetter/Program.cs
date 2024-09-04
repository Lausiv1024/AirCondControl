internal class Program
{
    const int DEVIDE_VALUE = 400;
    const int PER_TICK = 425;
    const int ERR = 75;

    static readonly string pulseHeader = "pulse ";
    static readonly string spaceHeader = "space ";

    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Arguments Required!  ByteDataGetter.exe <FilePath>");
            return;
        }
        if (!File.Exists(args[0]))
        {
            Console.WriteLine("File Not Found!");
            return;
        }
        List<int> Signals = new List<int>();
        int count = 0;
        int dataByte = 0;
        //もう逐次読み込みにしちゃったよ
        using (StreamReader sr = new StreamReader(args[0]))
        {
            while (sr.Peek() >= 0)
            {
                bool isTrailer = false;
                //基本pulse n,space n,pulse n,...となるのでその順序で読み込む
                string? pulseStr = sr.ReadLine();//pulse nullになる可能性は低い
                string? spaceStr = sr.ReadLine();//space トレーラーが奇数で終わる場合があるのでnullになる加茂

                if (spaceStr == null || spaceStr.Contains("timeout"))//spaceがない or timeout表記ならトレーラーとして認識
                {
                    isTrailer = true;
                }
                int pt = int.Parse(pulseStr.Replace(pulseHeader, string.Empty)) / DEVIDE_VALUE;//文字列データ(例：pulse 114514)を数値に変換し、Tに変換
                int pulse = int.Parse(pulseStr.Replace(pulseHeader, string.Empty));
                int st = 0;
                if (!isTrailer) st = int.Parse(spaceStr.Replace(spaceHeader, string.Empty)) / DEVIDE_VALUE;
                int space = 0;
                if (!isTrailer) space = int.Parse(spaceStr.Replace(spaceHeader, string.Empty));
                //2回目の赤外線信号がある場合があるため、次の信号との大きなブランクをトレーラーとして認識させる処理は必須
                if (space > PER_TICK * 8)
                {
                    isTrailer = true;
                }

                if (!isTrailer)
                {
                    if (IsProvidedTick(pulse, 8) && IsProvidedTick(space, 4))
                    {
                        Console.WriteLine("Reader");
                    } else if (IsProvidedTick(pulse, 1))
                    {
                        if (IsProvidedTick(space, 3))
                        {
                            dataByte += (int)Math.Pow(2, count);
                        }
                        count++;
                        if (count == 8)
                        {
                            Signals.Add(dataByte);
                            dataByte = 0;
                            count = 0;
                        }
                    }
                }
                else
                {
                    if (pt > 1)
                        break;
                    foreach (int i in Signals)
                    {
                        Console.Write(i > 15 ? Convert.ToString(i, 16) : $"0{Convert.ToString(i, 16)}");
                    }
                    Signals.Clear();
                    count = 0;
                    dataByte = 0;
                    Console.WriteLine();
                    Console.WriteLine("Trailer");
                }
            }
        }
    }

    private static bool IsProvidedTick(int pulse, int tick)
    {
        return NearlyEquals(pulse, tick * PER_TICK, ERR);
    }

    /// <summary>
    /// いわゆるニアリーイコールってやつ
    /// </summary>
    /// <param name="val">参照値</param>
    /// <param name="exa">ピッタリの値</param>
    /// <param name="err">許容誤差</param>
    /// <returns></returns>
    private static bool NearlyEquals(int val, int exa, int err)
    {
        int subtraction = Math.Abs(val - exa);
        return subtraction <= err;
    }
}