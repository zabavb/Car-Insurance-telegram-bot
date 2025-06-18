<p align="center">
	<img src="https://img.icons8.com/3d-fluency/100/chatbot.png" alt="Bot Logo" width="200"/>
</p>
<h1 align="center">Car Insurance Telegram Bot</h1>

<h2>🖥️ Overview</h2>
<p>The Car Insurance Telegram Bot is an interactive chatbot designed to guide users through the process of submitting personal documents (passport and vehicle ID), extracting essential information via OCR, confirming the details, and issuing a car insurance policy. It integrates with third-party services such as Mindee for OCR and Hugging Face for AI assistants.</p>

<hr />

<h2>🧱 Technologies Used</h2>
<ul>
  <li><strong>.NET 9.0</strong></li>
  <li><strong>Telegram.Bot (v22)</strong></li>
  <li><strong>Mindee OCR SDK</strong></li>
  <li><strong>QuestPDF</strong> for PDF generation</li>
  <li><strong>HuggingFace API</strong></li>
  <li><strong>Microsoft.Extensions.Hosting</strong> & Dependency Injection</li>
</ul>

<hr />

<h2>🛠️ Project Structure</h2>
<ul>
  <li><code>Services/</code>: Core services like <code>BotService</code>, <code>OcrService</code>, <code>ChatService</code>, <code>PolicyService</code></li>
  <li><code>Models/</code>: Domain models like <code>ConversationState</code>, <code>ExtractedData</code></li>
  <li><code>Options/</code>: Configuration binding classes</li>
  <li><code>Interfaces/</code>: Service abstractions</li>
</ul>

<hr />

<h2>🚦 Bot Workflow</h2>

<h3>1. Start Command</h3>
<ul>
  <li>User sends <code>/start</code></li>
  <li>Bot asks for passport photo</li>
</ul>

<h3>2. Passport Upload</h3>
<ul>
  <li>Bot saves uploaded passport</li>
  <li>Bot requests vehicle ID image</li>
</ul>

<h3>3. Vehicle Document Upload</h3>
<ul>
  <li>Bot saves image</li>
  <li>OCR is performed via Mindee</li>
  <li>Data is displayed with a <code>Yes/No</code> inline keyboard</li>
</ul>

<h3>4. User Confirms Data</h3>
<ul>
  <li><strong>Yes</strong>: Proceed to pricing</li>
  <li><strong>No</strong>: Ask for resubmission</li>
</ul>

<h3>5. User Accepts Price</h3>
<ul>
  <li>Bot generates and sends PDF policy</li>
</ul>

<h3>6. AI Assistant</h3>
<ul>
  <li>Bot continues with GPT-style chat</li>
</ul>

<hr />

<h2>🗝️ Key Services</h2>

<h3>BotService</h3>
<p>Implements <code>IHostedService</code>, orchestrates Telegram updates and bot flow.</p>

<h3>ConversationStore</h3>
<p>In-memory per-user state tracking.</p>

<h3>OcrService</h3>
<p>Uses Mindee API to extract user identity from passport images.</p>

<h3>PolicyService</h3>
<p>Generates PDF policies using QuestPDF.</p>

<h3>ChatService</h3>
<p>Handles user Q&A via Hugging Face.</p>

<hr />

<h2>⚙️ Configuration</h2>

<pre><code>"Telegram": {
    "BotToken": "YOUR_BOT_TOKEN_HERE"
  },
  "Mindee": {
    "ApiKey": "YOUR_MINDEE_API_KEY"
  },
  "HuggingFace": {
    "ApiUrl": "https://api-inference.huggingface.co/models/HuggingFaceH4/zephyr-7b-beta",
    "ApiToken": "YOUR_HF_API_TOKEN"
  }
</code></pre>

<hr />

<h2>💉 Dependency Injection</h2>

<pre><code>
services.AddSingleton&lt;IConversationStore, ConversationStore&gt;();
services.AddSingleton&lt;IPolicyService, PolicyService&gt;();
services.AddSingleton&lt;IOcrService, OcrService&gt;();
services.AddHttpClient&lt;IChatService, ChatService&gt;();
services.AddHostedService&lt;BotService&gt;();
</code></pre>

<hr />

<h2>❎ Error Handling</h2>
<ul>
  <li>AI, OCR, and Telegram errors are logged via <code>ILogger</code></li>
  <li>Graceful fallback messages are returned to the user</li>
</ul>

<hr />

<h2>📃 License</h2>
<p>This bot uses <strong>QuestPDF</strong> and <strong>Mindee SDK</strong> which require compliance with their respective licenses.</p>

<hr />

<h2>🤝 Author & Credits</h2>
<p>Developed by Viktor Bilonizhka using modern .NET practices and Telegram.Bot SDK 22.</p>
