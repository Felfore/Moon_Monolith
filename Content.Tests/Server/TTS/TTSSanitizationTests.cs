using Content.Server.TTS;
using NUnit.Framework;

namespace Content.Tests.Server.TTS
{
    [TestFixture]
    public sealed class TTSSanitizationTests
    {
        [Test]
        [TestCase("I'm", "I'm")]
        [TestCase("I'll", "I'll")]
        [TestCase("don't", "don't")]
        [TestCase("It's", "It's")]
        [TestCase("Curly’s", "Curly’s")]
        [TestCase("Clean text.", "Clean text.")]
        public void TestSanitization(string input, string expected)
        {
            var result = TTSSystem.Sanitize(input);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("Hello @world!", "Hello world!")]
        [TestCase("Price: $100", "Price one hundred")]
        public void TestStripping(string input, string expected)
        {
            var result = TTSSystem.Sanitize(input);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("Привет, как дела?", "Привет, как дела?")]
        [TestCase("Ще не вмерла!", "Ще не вмерла!")]
        // Regex: [^a-zA-Zа-яА-ЯёЁ0-9-Є-ЯҐа-їґ,\-+?!. '’]
        // ! IS allowed.
        public void TestCyrillic(string input, string expected)
        {
            var result = TTSSystem.Sanitize(input);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("123", "one hundred twenty three")]
        [TestCase("0", "zero")]
        public void TestNumbers(string input, string expected)
        {
            var result = TTSSystem.Sanitize(input);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("Hello!!!!!!", "Hello!")]
        [TestCase("What???", "What?")]
        [TestCase("Spam........", "Spam...")]
        [TestCase("Pause..", "Pause..")]
        [TestCase("Ellipsis...", "Ellipsis...")]
        [TestCase("Mixed!?!?", "Mixed!?!?")]
        [TestCase("I''''m", "I'm")]
        [TestCase("Curly’’’s", "Curly's")]
        public void TestPunctuationSquashing(string input, string expected)
        {
            var result = TTSSystem.Sanitize(input);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("Heeeeeellooooo", "Heelloo")]
        [TestCase("Aaaaaaa!!! BBBB", "Aaa! BB")]
        [TestCase("111111", "one hundred eleven thousand one hundred eleven")]
        [TestCase("Word     Word", "Word  Word")]
        public void TestGlobalDeSpamming(string input, string expected)
        {
            var result = TTSSystem.Sanitize(input);
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
