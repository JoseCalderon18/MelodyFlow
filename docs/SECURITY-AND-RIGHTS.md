# Security and rights

- OAuth2 for YouTube; confidential refresh/access tokens in encrypted credential store or secrets manager, not `YouTubeChannel` row plaintext.
- OpenRouter keys and n8n webhook keys never stored in entity catalog examples or version control; redact logging.
- Manual Google Flow visuals require proof of usage rights and compliance with current terms. SoundFont instruments/samples need explicit commercial-use license review.
- An algorithmic MIDI composer does not guarantee originality, Content ID clearance, or YouTube monetization eligibility. Review repetitive/reused-content and altered/synthetic content policies honestly; do not design features for hiding required disclosures.
- Validate every uploaded MIME, size, signature, duration; store outside webroot/DB; scan as appropriate; sanitize paths and isolate subprocess execution.
- The current local-only API has no user login, Identity, JWT or API key. Development launch profiles bind to localhost, CORS allows only configured Web App origins, a global per-IP limiter allows 120 requests/minute, and `[ApiController]` validates request models. Do not expose the unauthenticated API beyond the local machine. Add user authentication only as a separately approved feature if the deployment model changes.
- Keep structured logs free of credentials and sensitive prompts. The local n8n workflow calls the loopback API without credentials; do not expose it remotely. Any future remote service or webhook integration needs its own authentication, authorization and replay protection before it is enabled.
- Before publication store rights evidence and manual approval when applicable.
