// Verifica SetUp, TearDown, BodiesExactlyMatchApprovedSource, MetadataPreservesDatesSubjectsAndUnspecifiedHours, HumanizedEmailsKeepTheCluesAndHelenasTwoVoices, ChildParagraphsAreStyleMetadataAndAuthorNotesAreAbsent, OnlyEmailSevenContainsTheThreeSpecifiedAttachmentsWithoutInventedArt, ContentFactoriesDoNotShareMutableMessageParagraphOrAttachmentData, ValidationReportsMalformedIdsDatesBodiesAndAttachments.

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace HorrorGame.Tests
{
    [Category("BeforeDemolitionEmail")]
    public sealed class EmailCatalogTests
    {
        private EmailCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<EmailCatalog>();
            catalog.messages = EmailCatalog.BeforeDemolitionContent();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(catalog);

        // Humanização autorizada em 29/10: hashes do texto revisado no changelog. Metadados e pistas têm asserts independentes abaixo.
        [TestCase("email.01", "473b5757646679aeb7f86c55b98860dfc198e0d4e13e9c2d1cc6019edb340406")]
        [TestCase("email.02", "76d6e746750bd1e51fd09f0642c466fa301c569888499cdb6c182690db7c8635")]
        [TestCase("email.03", "7fc9c5a030a97123fb7a1d4af008891132589bbf1108b948e15c94719452e81e")]
        [TestCase("email.04", "22c1d75de9d21bb8c906323076d394b036779d032b1115b769e15cc74a1f8fac")]
        [TestCase("email.05", "596db9ffe6a5c3e7e6fe589e32aec5e2c739dce25e966f778aa9399db7be2441")]
        [TestCase("email.06", "637ac889a71d53b6adf94fac4b9baa36d2546f0e8c430a0b575257cccd50336b")]
        [TestCase("email.07", "c1e6dfaaebd2a12956ef8d6f1b464b0b913b3516a09eb549b0e2ffdfffa8672e")]
        [TestCase("email.08", "7fe615863955063d5a8e0b19516cf23277f759f912927aa31e94b1de6aa99a13")]
        public void BodiesExactlyMatchApprovedSource(string id, string expected)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(catalog.Find(id).PlainText));
                Assert.That(BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(), Is.EqualTo(expected), id);
            }
        }

        [Test]
        public void MetadataPreservesDatesSubjectsAndUnspecifiedHours()
        {
            Assert.That(catalog.ValidateContent(), Is.Empty);
            CollectionAssert.AreEqual(Enumerable.Range(1, 8).Select(number => "email." + number.ToString("00")),
                catalog.messages.Select(message => message.id));
            CollectionAssert.AreEqual(new[]
            {
                "14/03/2006", "09/04/2007", "22/11/2010", "03/06/2014",
                "17/09/2016", "08/05/2018", "02/10/2022", "26/10/2026"
            }, catalog.messages.Select(message => message.date));
            CollectionAssert.AreEqual(new[] { "", "", "03:07", "03:07", "03:07", "03:07", "03:07", "03:07" },
                catalog.messages.Select(message => message.time));
            CollectionAssert.AreEqual(new[]
            {
                "Alunos da 7B, podemos conversar?", "RE: Alunos da 7B", "desculpa o horário", "o senhor leu",
                "eu to bem", "Peço desculpas pelos emails anteriores", "Demolição programada", ""
            }, catalog.messages.Select(message => message.subject));
            Assert.That(catalog.messages.All(message => message.sender == "Helena Souza"), Is.True);
            Assert.That(catalog.messages.Take(7).All(message => message.initiallyDelivered), Is.True);
            Assert.That(catalog.Find("email.08").initiallyDelivered, Is.False);
            Assert.That(catalog.Find("missing"), Is.Null);
            Assert.That(catalog.Find(null), Is.Null);
        }

        [Test]
        public void HumanizedEmailsKeepTheCluesAndHelenasTwoVoices()
        {
            foreach(var clue in new[]{"SEEPE","quinta","mochila, casaco e caderno","Marta","caderno de desenho","204"})Assert.That(catalog.Find("email.01").PlainText,Does.Contain(clue));
            foreach(var clue in new[]{"diretor Aurélio","lancheira da Marta","desenho novo","Joguei fora.","Voltou pra gaveta no dia seguinte."})Assert.That(catalog.Find("email.02").PlainText,Does.Contain(clue));
            foreach(var clue in new[]{"2008","duas quadras","quatro pares","pegadas pequenas","fim do corredor."})Assert.That(catalog.Find("email.03").PlainText,Does.Contain(clue));
            foreach(var clue in new[]{"03:07","Bia","mão esquerda no mouse","polegar da direita"})Assert.That(catalog.Find("email.04").PlainText,Does.Contain(clue));
            Assert.That(catalog.Find("email.05").PlainText,Does.Contain("eu to bem").And.Contain("(Helena) (Helena)"),"The intentional breakdown must not be polished away.");
            foreach(string id in new[]{"email.06","email.07"})Assert.That(catalog.Find(id).PlainText,Does.StartWith("Prezado Sr. João,").And.Contain("Atenciosamente"));
            foreach(var clue in new[]{"apito do Luan","segunda gaveta","atrás da garrafa"})Assert.That(catalog.Find("email.06").PlainText,Does.Contain(clue));
            foreach(var clue in new[]{"27 de outubro","8h","relatório de 2006","Venha antes da demolição","Eles esperam o senhor","Marta","12 anos"})Assert.That(catalog.Find("email.07").PlainText,Does.Contain(clue));
        }

        [Test]
        public void ChildParagraphsAreStyleMetadataAndAuthorNotesAreAbsent()
        {
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 3, 7, 9, 5, 4 },
                catalog.messages.Select(message => message.paragraphs.Length));
            CollectionAssert.AreEqual(new[] { "email.05:2", "email.05:4", "email.05:5", "email.06:7", "email.06:8", "email.08:2" },
                catalog.messages.SelectMany(message => message.paragraphs.Select((paragraph, index) =>
                    new { message.id, paragraph.child, index })).Where(paragraph => paragraph.child)
                    .Select(paragraph => paragraph.id + ":" + paragraph.index));
            foreach (var message in catalog.messages)
            foreach (var forbidden in new[] { "[infantil]", "(Detalhe:", "Anexos:", "Daniel", "Hospício", "113" })
                Assert.That(message.PlainText, Does.Not.Contain(forbidden), message.id);
        }

        [Test]
        public void OnlyEmailSevenContainsTheThreeSpecifiedAttachmentsWithoutInventedArt()
        {
            Assert.That(catalog.messages.Where(message => message.id != "email.07")
                .All(message => message.attachments.Length == 0), Is.True);
            var attachments = catalog.Find("email.07").attachments;
            CollectionAssert.AreEqual(new[] { "relatorio_2006.pdf", "relatorio_2via.pdf", "foto_escritorio.jpg" },
                attachments.Select(attachment => attachment.fileName));
            CollectionAssert.AreEqual(new[] { true, true, false }, attachments.Select(attachment => attachment.unavailable));
            Assert.That(attachments.All(attachment => attachment.preview == null), Is.True);
        }

        [Test]
        public void ContentFactoriesDoNotShareMutableMessageParagraphOrAttachmentData()
        {
            var other = EmailCatalog.BeforeDemolitionContent();
            other[0].sender = "changed";
            other[0].paragraphs[0].text = "changed";
            other[6].attachments[0].fileName = "changed";
            Assert.That(catalog.Find("email.01").sender, Is.EqualTo("Helena Souza"));
            Assert.That(catalog.Find("email.01").paragraphs[0].text, Does.StartWith("Sr. João, boa tarde."));
            Assert.That(catalog.Find("email.07").attachments[0].fileName, Is.EqualTo("relatorio_2006.pdf"));
        }

        [Test]
        public void ValidationReportsMalformedIdsDatesBodiesAndAttachments()
        {
            catalog.messages[1].id = catalog.messages[0].id;
            catalog.messages[2].sender = " ";
            catalog.messages[3].date = "31/02/2014";
            catalog.messages[4].time = "25:70";
            catalog.messages[5].paragraphs = new[] { new EmailParagraph { text = " " }, null };
            catalog.messages[6].attachments = new[] { new EmailAttachment { fileName = " " }, null };
            catalog.messages[7] = null;
            var errors = catalog.ValidateContent();
            Assert.That(errors, Has.Some.Contains("ID único"));
            Assert.That(errors, Has.Some.Contains("remetente"));
            Assert.That(errors, Has.Some.Contains("Data"));
            Assert.That(errors, Has.Some.Contains("Hora"));
            Assert.That(errors, Has.Some.Contains("Corpo"));
            Assert.That(errors, Has.Some.Contains("Anexo"));
            Assert.That(errors, Has.Some.Contains("nulo"));
            catalog.messages = null;
            Assert.That(catalog.ValidateContent(), Is.Not.Empty);
            Assert.That(catalog.Find("email.01"), Is.Null);
        }
    }
}
