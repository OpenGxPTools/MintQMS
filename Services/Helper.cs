/*
This file is part of the OpenQMS.net project (https://github.com/C-realize/OpenQMS).
Copyright (C) 2022-2024  C-realize IT Services SRL (https://www.c-realize.com)

This program is offered under a commercial and under the AGPL license.
For commercial licensing, contact us at https://www.c-realize.com/contact.  For AGPL licensing, see below.

AGPL:
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see http://www.gnu.org/licenses/.
*/

using iText.Bouncycastle.Crypto;
using iText.Bouncycastle.X509;
using iText.Kernel.Pdf;
using iText.Signatures;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pkcs;
//using Org.BouncyCastle.X509;
using static iText.Signatures.PdfSigner;

namespace OpenQMS.Services
{
    public static class Helper
    {
        public static /*string*/byte[] SignPDF(/*string filePath*/byte[] fileContent, string? certPassword)
        {
            MemoryStream pdfStream = new MemoryStream(fileContent);
            PdfReader reader = new PdfReader(/*filePath*/pdfStream);
            //var basePath = Path.Combine(Directory.GetCurrentDirectory() + "\\Files\\");
            //string[] pathArr = filePath.Split('\\');
            //string fileName = pathArr.Last();
            //string signedFile = $"{basePath}Signed_{fileName}";

            //FileStream file = new FileStream(signedFile, FileMode.Create, FileAccess.ReadWrite);
            MemoryStream outputStream = new MemoryStream();
            PdfSigner signer = new PdfSigner(reader, /*file*/outputStream, new StampingProperties());

            string KEYSTORE = Directory.GetCurrentDirectory() + "/cert.pfx";
            if (string.IsNullOrEmpty(certPassword))
                throw new InvalidOperationException(
                    "Signing certificate password is not configured. Set 'CertPassword' via " +
                    "user-secrets (dotnet user-secrets set CertPassword <password>) or the " +
                    "CertPassword environment variable.");
            char[] PASSWORD = certPassword.ToCharArray();

            Pkcs12Store pk12 = new Pkcs12StoreBuilder().Build();
            pk12.Load(new FileStream(KEYSTORE, FileMode.Open, FileAccess.Read), PASSWORD);
            string alias = null;
            foreach (var a in pk12.Aliases)
            {
                alias = ((string)a);
                if (pk12.IsKeyEntry(alias))
                    break;
            }
            ICipherParameters pk = pk12.GetKey(alias).Key;

            X509CertificateEntry[] ce = pk12.GetCertificateChain(alias);
            X509CertificateBC[] chain = new X509CertificateBC[ce.Length];
            for (int k = 0; k < ce.Length; ++k)
            {
                chain[k] = new X509CertificateBC(ce[k].Certificate);
            }

            // Invisible signature appearance — no visible box on page
            PdfSignatureAppearance appearance = signer.GetSignatureAppearance();
            appearance.SetReuseAppearance(false);
            signer.SetFieldName("DigitalSignature_" + DateTime.UtcNow.Ticks);

            IExternalSignature pks = new PrivateKeySignature(new PrivateKeyBC(pk), DigestAlgorithms.SHA256);
            signer.SignDetached(pks, chain, null, null, null, 0, CryptoStandard.CMS);

            byte[] signedFile = outputStream.ToArray();
            return signedFile;
        }
    }
}
