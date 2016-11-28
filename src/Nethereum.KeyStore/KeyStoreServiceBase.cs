﻿using System;
using Nethereum.KeyStore.Crypto;
using Nethereum.KeyStore.Model;
using Newtonsoft.Json;

namespace Nethereum.KeyStore
{
    public abstract class KeyStoreServiceBase<T> : IKeyStoreService<T> where T : KdfParams
    {
        protected readonly IRandomBytesGenerator RandomBytesGenerator;
        protected readonly KeyStoreCrypto KeyStoreCrypto;
        public const int CurrentVersion = 3;

        protected KeyStoreServiceBase() : this(new RandomBytesGenerator(), new KeyStoreCrypto())
        {

        }

        protected KeyStoreServiceBase(IRandomBytesGenerator randomBytesGenerator, KeyStoreCrypto keyStoreCrypto)
        {
            RandomBytesGenerator = randomBytesGenerator;
            KeyStoreCrypto = keyStoreCrypto;
        }

        public KeyStore<T> EncryptAndGenerateKeyStore(string password, byte[] privateKey, string address) 
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (privateKey == null) throw new ArgumentNullException(nameof(privateKey));
            if (address == null) throw new ArgumentNullException(nameof(address));

            if(privateKey.Length != 32) throw new ArgumentException("Private key should be 32 bytes", nameof(privateKey));

            var salt = RandomBytesGenerator.GenerateRandomSalt();
         
            var kdfParams = GetDefaultParams();

            var derivedKey = GenerateDerivedKey(KeyStoreCrypto.GetPasswordAsBytes(password), salt, kdfParams);

            var cipherKey = KeyStoreCrypto.GenerateCipherKey(derivedKey);

            var iv = RandomBytesGenerator.GenerateRandomInitialisationVector();

            var cipherText = GenerateCipher(privateKey, iv, cipherKey);

            var mac = KeyStoreCrypto.GenerateMac(derivedKey, cipherText);

            var cryptoInfo = new CryptoInfo<T>(GetCipherType(), cipherText, iv, mac, salt, kdfParams, GetKdfType());

            var keyStore = new KeyStore<T>
            {
                Version = CurrentVersion,
                Address = address,
                Id = Guid.NewGuid().ToString(),
                Crypto = cryptoInfo
            };

            return keyStore;

        }

        public string EncryptAndGenerateKeyStoreAsJson(string password, byte[] privateKey, string addresss)
        {
            var keyStore = EncryptAndGenerateKeyStore(password, privateKey, addresss);
            return JsonConvert.SerializeObject(keyStore);
        }

        public KeyStore<T> DeserializeKeyStoreFromJson(string json)
        {
            return JsonConvert.DeserializeObject<KeyStore<T>>(json);
        }

        public abstract byte[] DecryptKeyStore(string password, KeyStore<T> keyStore);

        public byte[] DecryptKeyStoreFromJson(string password, string json)
        {
            var keyStore = DeserializeKeyStoreFromJson(json);
            return DecryptKeyStore(password, keyStore);
        }

        protected virtual byte[] GenerateCipher(byte[] privateKey, byte[] iv, byte[] cipherKey)
        {
            return KeyStoreCrypto.GenerateAesCtrCipher(iv, cipherKey, privateKey);
        }

        public abstract string GetKdfType();

        protected abstract byte[] GenerateDerivedKey(byte[] pasword, byte[] salt, T kdfParams);

        protected abstract T GetDefaultParams();

        public virtual string GetCipherType()
        {
            return "aes-128-ctr";
        }

    }
}