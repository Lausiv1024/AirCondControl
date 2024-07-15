using System;
using System.Data;
using System.Runtime.CompilerServices;
internal class Program
{
    const int DEVIDE_VALUE = 400;
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
                int pt = int.Parse(pulseStr.Replace("pulse ", "")) / DEVIDE_VALUE;//文字列データ(例：pulse 114514)を数値に変換し、Tに変換
                int st = 0;
                if (!isTrailer) st = int.Parse(spaceStr.Replace("space ", "")) / DEVIDE_VALUE;
                if (st > 8)
                {
                    isTrailer = true;
                }

                if (!isTrailer)
                {
                    if (pt == 8 && st == 4)
                    {
                        Console.WriteLine("Reader");
                    } else if (pt <= 1)
                    {
                        if (st == 3)
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
                    foreach(int i in Signals)
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
}