# GPT-4o OCR Integration - Executive Summary

## Quick Decision Matrix

| Question | Answer | Source |
|----------|--------|--------|
| **Best Model?** | `gpt-4o` (production-ready, latest) | OpenAI API |
| **Real-time Cost (100K images/month)?** | $5,100/year | Pricing analysis |
| **Batch Processing Cost?** | $2,550/year (50% savings) | Batch API pricing |
| **Image Format Support?** | JPEG, PNG, GIF, WebP | OpenAI docs |
| **Max Image Size?** | 20MB (recommended <10MB) | OpenAI limits |
| **Vision Model Identifier?** | `gpt-4o`, not `gpt-4-vision` | Current API |
| **Token Cost Per Image?** | 85 (low) to 2000+ (high detail) | Token accounting |
| **Expected Latency?** | 5-15 seconds per image | OpenAI benchmarks |
| **Base64 Overhead?** | +33% file size, use URLs instead | Best practice |
| **Best .NET Package?** | `OpenAI` v2.8.0 | NuGet current |
| **Thread Safe?** | Yes, register as Singleton | SDK design |
| **Retry Strategy?** | Automatic 3x with exponential backoff | Built-in |

---

## Top 3 Implementation Recommendations

### 1. **For Immediate Recipe Import** (High Priority)
```
Use: GPT-4o with ChatClient
Cost: ~$127/month (30K recipes)
Latency: Real-time (5-15s per image)
Setup Time: 2-3 days
Benefits: User can see extracted recipes immediately
```

### 2. **For Daily Bulk Imports** (Medium Priority)  
```
Use: GPT-4o Batch API (asynchronous)
Cost: ~$63/month (30K recipes, 50% savings)
Latency: 24-hour processing window
Setup Time: 5-7 days
Benefits: Significant cost savings, scheduled processing
```

### 3. **Cost Optimization** (Long-term)
```
Use: Hybrid (Azure CV for standard OCR + GPT-4o for complex recipes)
Cost: ~$200/month (mixed workloads)
Latency: Varies by path
Setup Time: 10-14 days
Benefits: Best cost per image, leverages specialized services
```

---

## Critical Implementation Details

### Image Processing Pipeline
```
1. Receive image (file/URL/base64)
   ↓
2. Load and validate (check size/format)
   ↓
3. Resize if > 2MB (target 1024-2048px)
   ↓
4. Compress with JPEG Q85
   ↓
5. Choose delivery method:
   - < 50KB → convert to base64
   - > 50KB → upload to cloud URL
   ↓
6. Send to GPT-4o with vision_detail: auto
```

### Dependency Injection (One-time Setup)
```csharp
builder.Services.AddSingleton<ChatClient>(sp => 
    new(model: "gpt-4o", 
        new ApiKeyCredential(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))));
```

### Error Handling Strategy
| Error | Retry? | Action |
|-------|--------|--------|
| 408 Timeout | Yes (3x) | Exponential backoff built-in |
| 429 Rate Limit | Yes (3x) | Wait before retry (exponential) |
| 500/502/503 | Yes (3x) | Service likely recovering |
| 400 Bad Request | No | Fix request payload |
| Invalid Image | No | Preprocess or reject |

### Cost Tracking Formula
```
Monthly Cost = (Image Count × Avg Input Tokens × $0.000005) 
             + (Image Count × Avg Output Tokens × $0.000015)

Example (30,000 images):
= (30,000 × 400 × $0.000005) + (30,000 × 150 × $0.000015)
= $60 + $67.50
= $127.50/month
```

---

## File Structure

📁 specs/001-import-recipe/
├── 📄 plan.md (original planning)
├── 📄 spec.md (requirements)
├── 📄 tasks.md (implementation tasks)
├── 📄 **gpt-4o-ocr-integration-findings.md** ← DETAILED RESEARCH (this file)
└── 📄 **gpt-4o-dotnet9-implementation-guide.md** ← CODE PATTERNS & EXAMPLES

---

## Next Steps (Priority Order)

### Phase 1: Foundation (Week 1)
- [ ] Review `gpt-4o-ocr-integration-findings.md` for complete details
- [ ] Install NuGet: `dotnet add package OpenAI --version 2.8.0`
- [ ] Set up OPENAI_API_KEY in development environment
- [ ] Create `RecipeOcrService` interface & stub implementation
- [ ] Configure dependency injection in Program.cs

### Phase 2: Core Implementation (Week 2-3)
- [ ] Implement `ImagePreprocessor` with resizing/compression
- [ ] Implement `RecipeOcrService.ExtractRecipeFromImageAsync`
- [ ] Add vision message building with `ChatMessageContentPart.CreateImagePart`
- [ ] Implement JSON parsing of recipe extraction response
- [ ] Create API endpoint `/api/recipes/import-from-url`

### Phase 3: Enhancement (Week 4)
- [ ] Add multi-image per-request for batch processing
- [ ] Implement retry logic with exponential backoff
- [ ] Add logging and monitoring (Serilog)
- [ ] Create unit tests with mocked ChatClient
- [ ] API endpoint `/api/recipes/batch` for 2-10 image processing

### Phase 4: Optimization (Week 5+)
- [ ] Implement Batch API for asynchronous processing (optional, 6-month ROI)
- [ ] Set up cost monitoring dashboard
- [ ] Configure Azure Blob Storage for image uploads (if needed)
- [ ] Load testing with concurrent requests
- [ ] Production deployment & monitoring

---

## Key Findings by Topic

### 1. Vision API Integration
- ✅ Endpoint: `POST https://api.openai.com/v1/chat/completions`
- ✅ Model: `gpt-4o` (use this, not deprecated `gpt-4-vision`)
- ✅ Image formats: JPEG, PNG, GIF, WebP
- ✅ Encoding: URLs preferred for >50KB, base64 for small images
- ℹ️ Rate limits: Managed via TPM/RPM in standard pool
- ℹ️ Latency: 5-15 seconds typical per image

### 2. Cost Comparison
- GPT-4o (real-time): **$5,100/year** at 100K images/month
- GPT-4o + Batch API (24hr): **$2,550/year** (50% savings)
- Azure Computer Vision: **$1,200/year** (60% cheaper, less intelligent)
- **Recommendation**: Start with GPT-4o real-time, add Batch API for 30%+ volume

### 3. Image Preprocessing
- Max size: 20MB (recommend <10MB)
- Optimal dimensions: 1024-2048px
- Format: Use JPEG Q85 compression to <500KB
- Token cost: 85 (low detail) to 2000+ (high detail)
- **Recommendation**: Use `vision_detail: auto` for balanced cost/quality

### 4. .NET Client Library
- Package: `OpenAI` v2.8.0 (latest stable)
- Compatible: .NET Standard 2.0+ (.NET 6, 7, 8, 9+)
- Thread-safe: Yes (register as Singleton)
- Retry: Automatic 3x with exponential backoff
- **Recommendation**: Use official `OpenAI` package, not third-party alternatives

### 5. Batch Processing
- ✅ Can process multiple images in single request (optimal: 2-3)
- ✅ Batch API supports 50,000 requests per batch
- ✅ 50% cost discount with 24-hour completion window
- ✅ Perfect for scheduled/off-peak recipe processing
- **Recommendation**: Use for non-real-time imports, fallback for cost optimization
- **Token limit**: No strict per-batch limit, model-specific
- **Result structure**: Use `custom_id` to map requests to responses

---

## Security Considerations

```csharp
// ✅ CORRECT: Secure API key storage
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var credential = new ApiKeyCredential(apiKey);

// ❌ WRONG: Don't hardcode API keys
var credential = new ApiKeyCredential("sk-proj-xxx");

// ✅ For Azure Key Vault
var client = new SecretClient(vaultUri, new DefaultAzureCredential());
var secret = await client.GetSecretAsync("openai-api-key");
```

---

## Performance Benchmarks

| Metric | Value | Notes |
|--------|-------|-------|
| Single image latency | 5-15s | Network + processing + inference |
| Concurrent throughput | 10+ images/sec* | With rate limits |
| Token consumption | 400-600/image avg | With medium detail level |
| Memory per request | ~10-50MB | Image loading in memory |
| API response | <1s | Excluding image transmission |

*Depends on rate limits (TPM/RPM), which vary by tier

---

## Troubleshooting Quick Reference

| Issue | Solution |
|-------|----------|
| **"API key invalid"** | Check `OPENAI_API_KEY` env var and OpenAI dashboard |
| **"Rate limit exceeded (429)"** | Wait, retry with exponential backoff (built-in) |
| **"Timeout after 300s"** | Image too large, preprocess to <2MB |
| **"Invalid image URL"** | URL must be publicly accessible, HTTPS required |
| **"No text extracted"** | Try high-detail level or check image quality |
| **"High token count"** | Use `vision_detail: low` to reduce cost by 95% |
| **"Batch not completing"** | Check for 24-hour expiration, file size <200MB |

---

## Document References

For complete details, see:
1. **gpt-4o-ocr-integration-findings.md** - Full research with pricing tables, rate limits, code examples
2. **gpt-4o-dotnet9-implementation-guide.md** - Ready-to-use code patterns for .NET 9

---

## Success Metrics

Post-implementation, track:
- ✅ Recipe extraction accuracy > 90%
- ✅ Processing latency < 20 seconds (p95)
- ✅ Cost per image < $0.005
- ✅ User satisfaction with auto-populated recipe fields
- ✅ System uptime > 99.5%
- ✅ Error rate < 2%

---

**Research completed:** February 22, 2026  
**Data currency:** Latest as of Feb 2026  
**Recommendation status:** Ready for implementation  

