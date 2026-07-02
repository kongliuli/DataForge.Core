using DataMigration.Contracts;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.DataEncryptionTransformer;

public class DataEncryptionTransformer : ITransformer
{
    public string Id => "DataMigration.Plugin.DataEncryptionTransformer";
    public string Name => "数据加密/解密转换器";
    public string Description => "支持对敏感数据进行加密或解密";
    public Version Version => new Version(1, 0, 0);

    private string _key;
    private string _iv;

    public Task ExecuteAsync(CancellationToken ct)
    {
        // 这个方法在 ITransformer 中不需要实现，因为转换是通过 TransformAsync 方法完成的
        return Task.CompletedTask;
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        // 初始化逻辑，这里可以为空
    }

    public async Task ShutdownAsync(CancellationToken ct)
    {
        // 关闭逻辑，这里可以为空
    }

    public async IAsyncEnumerable<DataRecord> TransformAsync(IAsyncEnumerable<DataRecord> data, TransformConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        // 获取配置参数
        var mode = config.TryGetValue("Mode", out var modeValue) ? modeValue.ToLower() : "encrypt";
        var fields = config.TryGetValue("Fields", out var fieldsValue) ? fieldsValue.Split(',') : Array.Empty<string>();
        _key = config.TryGetValue("Key", out var keyValue) ? keyValue : "DefaultEncryptionKey123";
        _iv = config.TryGetValue("IV", out var ivValue) ? ivValue : "DefaultIV12345678";

        // 确保密钥和 IV 的长度符合要求
        _key = PadOrTruncate(_key, 32); // AES-256 需要 32 字节密钥
        _iv = PadOrTruncate(_iv, 16);   // AES 需要 16 字节 IV

        // 处理每条数据记录
        await foreach (var record in data)
        {
            var transformedRecord = new DataRecord();

            // 复制原始记录的所有字段
            foreach (var kvp in record)
            {
                transformedRecord[kvp.Key] = kvp.Value;
            }

            // 对指定字段进行加密或解密
            foreach (var field in fields)
            {
                var fieldName = field.Trim();
                if (transformedRecord.TryGetValue(fieldName, out var value))
                {
                    try
                    {
                        if (mode == "encrypt")
                        {
                            var encryptedValue = Encrypt(value.ToString());
                            transformedRecord[fieldName] = encryptedValue;
                        }
                        else if (mode == "decrypt")
                        {
                            var decryptedValue = Decrypt(value.ToString());
                            transformedRecord[fieldName] = decryptedValue;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 如果加密或解密失败，可以选择跳过该字段或使用默认值
                        // 可以在这里添加日志记录
                    }
                }
            }

            yield return transformedRecord;
        }
    }

    private string Encrypt(string plainText)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(_key);
            aes.IV = Encoding.UTF8.GetBytes(_iv);

            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using (var msEncrypt = new System.IO.MemoryStream())
            {
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                using (var swEncrypt = new System.IO.StreamWriter(csEncrypt))
                {
                    swEncrypt.Write(plainText);
                }
                var encrypted = msEncrypt.ToArray();
                return Convert.ToBase64String(encrypted);
            }
        }
    }

    private string Decrypt(string cipherText)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(_key);
            aes.IV = Encoding.UTF8.GetBytes(_iv);

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using (var msDecrypt = new System.IO.MemoryStream(Convert.FromBase64String(cipherText)))
            using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
            using (var srDecrypt = new System.IO.StreamReader(csDecrypt))
            {
                return srDecrypt.ReadToEnd();
            }
        }
    }

    private string PadOrTruncate(string input, int length)
    {
        if (input.Length == length)
        {
            return input;
        }
        else if (input.Length > length)
        {
            return input.Substring(0, length);
        }
        else
        {
            return input.PadRight(length, '0');
        }
    }
}
