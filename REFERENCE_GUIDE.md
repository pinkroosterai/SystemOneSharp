# System 1 Decision Engine Reference Guide (Laya & Jev)

---

## 1. Input Tokens & Context Limits

Laya and Jev operate as single-pass decision engines. Input processing speed depends directly on payload size, making token budget management essential for maintaining sub-second inference.

### Token Limits by Checkpoint

| Checkpoint / Model | Default Context Limit | Maximum Extended Limit |
| --- | --- | --- |
| **`laya` (Standard English)** | 512 tokens | 8,192 tokens |
| **`laya-multilingual`** | 1,024 tokens | 8,192 tokens |
| **`laya-typed-decisions`** | 1,024 tokens | 8,192 tokens |
| **TypeSafe Jev (Cloud API)** | 4,096 tokens | 16,384 tokens |

---

### Token-to-Character Conversion Estimates

In standard English text, **1 token ≈ 4 characters** (including spaces and punctuation) or **~0.75 words**.

| Token Count | Chars (English) | Words (English) | Approximate Page Equivalent |
| --- | --- | --- | --- |
| **512 tokens** | ~2,000 – 2,200 | ~350 – 400 | 1 single-spaced page |
| **1,024 tokens** | ~4,000 – 4,500 | ~700 – 800 | 2 – 3 pages |
| **4,096 tokens** | ~16,000 – 18,000 | ~2,800 – 3,200 | 7 – 8 pages |
| **8,192 tokens** | ~32,000 – 36,000 | ~5,500 – 6,000 | 12 – 15 pages |

#### Key Context Rules

* **Shared Payload Budget:** The token limit applies to the **entire combined JSON payload**, including the input `state`, instruction strings, option keys, rubric labels, and JSON structure.
* **Non-English & Code Overhead:** Numbers, non-English text, raw JSON, and code snippets tokenize less efficiently, averaging **2 to 3 characters per token**.
* **Extending Bounds:** Laya supports up to 8,192 tokens via Rotary Position Embeddings (RoPE). Set `max_len=8192` in the server startup configuration to enable longer contexts without latency penalties on shorter inputs.

---

## 2. Instruction Engineering Guidelines

Writing instructions for non-generative System 1 models requires a structural shift from traditional LLM prompting. Because System 1 models evaluate candidate options via direct logit/softmax probability distribution rather than generating auto-regressive text, instructions must be dense, explicit, and bounded.

### Guidelines by Primitive Type

#### `choice` Primitives (Classification & Routing)

* **Define Option Trigger Conditions:** Always provide a clear description explaining *when* an option applies rather than relying on the key name alone.
* **Maintain Mutual Exclusivity:** Ensure category boundaries do not overlap (e.g., separate `password_reset` from `account_access`).
* **Include an Escape Route:** Always include an `other`, `unclear`, or `manual_review` option to capture inputs outside the target domain.
* **Keep Bounded Ranges:** Ideal choice counts range from **2 to 15 options**. Classification accuracy degrades when evaluating more than 50 choices simultaneously.

#### `score` Primitives (Ordinal Evaluation)

* **Explicitly Define Numeric Levels:** Assign clear operational definitions to each ordinal score in either the `instructions` or the `rubric` list.
* **Isolate Single Dimensions:** Do not evaluate multiple independent metrics in a single score (e.g., evaluate *urgency* and *technical severity* as two separate primitive questions).

#### `noul` Primitives (Boolean Probability Checks)

* **Use Direct Declarative Statements:** Write assertions as simple binary conditions (e.g., *"Does the text contain an explicit payment refund request?"*).
* **Avoid Negative Framing:** Do not use double negatives (e.g., *"Is this message not unhelpful?"*). Affirmative statements produce more accurate calibrated probabilities.

---

### Anti-Pattern Comparison

| Anti-Pattern (Generative LLM Habit) | Best Practice (System 1 Decision Engine) |
| --- | --- |
| **Persona Preamble:** *"You are an expert support agent..."* | **Omit Personas:** State judgment criteria directly without character framing. |
| **Chain-of-Thought:** *"Think step-by-step before answering."* | **Direct Criteria:** List explicit decision triggers; System 1 models cannot write thought traces. |
| **Boolean Casting:** Converting `noul` outputs directly to `true`/`false`. | **Thresholding:** Read `noul` as a float between `0.0` and `1.0` and apply custom logic thresholds in application code. |
| **Unbounded Inputs:** Omitting fallback options in `choice`. | **Explicit Fallbacks:** Always provide an `other` or `manual_review` category. |

---

## 3. Production Payload Example

```json
{
  "model": "laya-latest",
  "state": "System Log [2026-09-25 01:14:02]: Database connection pool exhausted on Node 4. API latency spiked to 4,200ms. 350 transactions failed.",
  "questions": {
    "is_outage_event": {
      "type": "noul",
      "instructions": "Does this log describe an active system service disruption or failure?"
    },
    "incident_severity": {
      "type": "score",
      "instructions": "Rate operational impact: 0 = Info/Warning, 1 = Minor Degradation, 2 = Major Incident, 3 = Critical Outage.",
      "rubric": ["0", "1", "2", "3"]
    },
    "escalation_target": {
      "type": "choice",
      "instructions": "Select the primary engineering team to notify based on the root failure.",
      "options": {
        "database_infra": "Database cluster failures, connection pooling, disk space, or SQL locks.",
        "network_security": "DDoS attacks, firewall blocks, or SSL certificate issues.",
        "application_dev": "Unhandled code exceptions, null pointers, or application logic bugs.",
        "unclear_escalate": "Ambiguous logs requiring triage by the general site reliability team."
      }
    }
  }
}

```
