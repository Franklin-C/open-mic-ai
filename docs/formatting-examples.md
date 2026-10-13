# Automatic formatting examples

[← Open Mic AI](../README.md)

Choose **Settings → Style → Polish**, then speak. The local AI improves phrasing and chooses the layout from your content. There is no format picker or wording toggle.

**Raw transcript** skips AI editing. **Clean up** fixes fillers, grammar and punctuation. **Polish** rewrites and formats automatically. **Custom** uses your prompt. Vocabulary corrections and saved snippets work in all four styles.

These are **actual outputs**, captured on Windows with Qwen3 4B Instruct 2507 (Q3_K_S) for the 3.7.0 update. Output text below has not been rewritten for this guide. These checks test editing of transcribed text, not microphone accuracy. Different models and phrasing can produce different results.

## Try an example

1. Select Polish and wait for your models to be ready.
2. Dictate an input below, or paste it into a text box, highlight it, press your shortcut and say “rewrite this”.
3. Finish recording. Open History to **Copy original** or **Copy modified**.

With selected text, “translate into French” and “summarize this” display a result card. “Rewrite this more clearly” replaces the selection. Dictating replacement text also replaces the selection.

## The 19 layouts

These are purposes the planner recognizes, not rigid templates. A blog, essay or proposal can use paragraphs; a normal sentence should stay a sentence. Polish preserves the supplied content rather than inventing a complete article from a topic.

| Layout | What to expect |
| --- | --- |
| [Sentences](#prose) | A short request or message stays ordinary prose. |
| [Paragraphs](#paragraphs) | A longer reflection separates related ideas into paragraphs. |
| [Lists](#list) | Items to collect become bullets; the corrected banana quantity replaces the earlier item. |
| [Goals](#goals) | Several aims become separate items, with the review date kept outside the list. |
| [Instructions](#steps) | An ordered procedure becomes numbered steps; warnings stay included. |
| [Recipes](#recipe) | Cooking actions become ordered steps, retaining quantities, timing and conditions. |
| [Reports and sections](#sections) | Supplied labels such as Progress and Blockers separate the report. |
| [Emails](#email) | The greeting, body and sign-off are separated. |
| [Tables](#table) | Repeated attributes across named items become rows and columns. |
| [Blog posts](#blog) | A personal article is grouped into paragraphs; no extra story is generated. |
| [Essays](#essay) | The argument and supporting ideas are grouped into paragraphs. |
| [Meeting notes](#notes) | Decisions, actions and unresolved details are separated for scanning. |
| [Checklists](#checklist) | A request for boxes to tick creates unchecked tasks. |
| [Outlines](#outline) | Main topics and their supplied subpoints form a nested list. |
| [Proposals](#proposal) | The suggestion, reason, scope and approval request are grouped into paragraphs. |
| [Agendas](#agenda) | Meeting topics follow the stated order, keeping owners and the time limit. |
| [Questions and answers / FAQs](#faq) | Multiple supplied question-and-answer pairs are separated; missing question marks are added. |
| [Social posts](#social) | A short caption stays compact; no hashtags or links are invented. |
| [Scripts and dialogue](#script) | Each supplied speaker turn starts on its own line. |

## What was verified

The broader local-model test passed **88 cases**: 57 dictated examples covering all 19 layouts with three examples each, 19 selected-text rewrites, eight ordinary messages, and vocabulary/snippet expansion in all four styles. Further table and outline checks covered four cases each after their repairs. All 19 baseline layouts also passed through the spoken “rewrite this” entry point. The checks cover routing, output shape, retained dates and supplied facts, quantities, uncertainty, warnings and duplicate sentences. The app also checks meaning before accepting Polish edits.

The FAQ below reproduces the reported failure with “question one. is…”. The grocery example checks “actually two bananas” replacing the earlier quantity. Recipe checks reject several cooking actions compressed into two dense steps; table and outline checks keep closing qualifications while removing duplicated source content.

These results do not guarantee every phrasing or every model. The small local model can preserve awkward setup phrases or choose a different paragraph grouping. If the result is wrong, save both texts and the model name so the failure can be reproduced.

GitHub renders the Markdown lists, checkboxes and tables below. Plain text fields may show the markers literally. Compatible editors can receive a rich table.

<a id="prose"></a>
## 1. Sentences

A short request or message stays ordinary prose.

**Input**

```text
Could you send the blue folder to Maya before Friday? Don't include the old invoice.
```

**Actual Polish output**

> Could you send the blue folder to Maya before Friday? Don't include the old invoice.

<a id="paragraphs"></a>
## 2. Paragraphs

A longer reflection separates related ideas into paragraphs.

**Input**

```text
I have been thinking about how I spend my evenings. Checking messages after dinner leaves me distracted, and I end up going to bed later than planned. Last week I put my phone in the kitchen and read instead. I slept better on those nights, but I missed a call from my sister. This week I will tell her to call the house phone if she needs me. I want to try this for another week before changing anything else.
```

**Actual Polish output**

> I have been thinking about how I spend my evenings. Checking messages after dinner leaves me distracted, and I end up going to bed later than planned.
>
> Last week I put my phone in the kitchen and read instead.
>
> I slept better on those nights, but I missed a call from my sister. This week I will tell her to call the house phone if she needs me.
>
> I want to try this for another week before changing anything else.

<a id="list"></a>
## 3. Lists

Items to collect become bullets; the corrected banana quantity replaces the earlier item.

**Input**

```text
For the picnic I need apples, bananas, pears and three bottles of water. Actually two bananas. Keep the drinks in a separate bag.
```

**Actual Polish output**

> For the picnic I need:
>
> - Apples
> - Two bananas
> - Pears
> - Three bottles of water
>
> Keep the drinks in a separate bag.

<a id="goals"></a>
## 4. Goals

Several aims become separate items, with the review date kept outside the list.

**Input**

```text
This month I aim to finish two books, save fifty dollars and walk for twenty minutes every morning. I will review how it went on the last Friday.
```

**Actual Polish output**

> This month I aim to:
>
> - Finish two books
> - Save fifty dollars
> - Walk for twenty minutes every morning
>
> I will review how it went on the last Friday.

<a id="steps"></a>
## 5. Instructions

An ordered procedure becomes numbered steps; warnings stay included.

**Input**

```text
To reset the printer first switch it off, then unplug it for thirty seconds, plug it back in and print a test page. Don't reset it while a job is printing.
```

**Actual Polish output**

> 1. Switch the printer off.
> 2. Unplug it for thirty seconds.
> 3. Plug it back in.
> 4. Print a test page.
> Do not reset it while a job is printing.

<a id="recipe"></a>
## 6. Recipes

Cooking actions become ordered steps, retaining quantities, timing and conditions.

**Input**

```text
For dinner wash the rice, put it in a pan with two cups of water, bring it to the boil, then reduce the heat and simmer for fifteen minutes. Leave the lid on while it cooks.
```

**Actual Polish output**

> 1. Wash the rice.
> 2. Place the rice in a pan with two cups of water.
> 3. Bring to a boil.
> 4. Reduce the heat and simmer for fifteen minutes.
> 5. Leave the lid on while it cooks.

<a id="sections"></a>
## 7. Reports and sections

Supplied labels such as Progress and Blockers separate the report.

**Input**

```text
Progress the beta is ready and the design is approved. Blockers the export fails on large files. Next actions Maya will check the logs on Friday and Lee will test the fix. The release date is still undecided.
```

**Actual Polish output**

> Progress\
> The beta is ready and the design is approved.
>
> Blockers\
> The export fails on large files.
>
> Next actions\
> - Maya will check the logs on Friday
> - Lee will test the fix
>
> The release date is still undecided.

<a id="email"></a>
## 8. Emails

The greeting, body and sign-off are separated.

**Input**

```text
Hi Maya, could you send the revised invoice before Friday? Please keep reference 7319 on it. Thanks, Alex.
```

**Actual Polish output**

> Hi Maya,
>
> Could you send the revised invoice before Friday? Please keep reference 7319 on it.
>
> Thanks,\
> Alex

<a id="table"></a>
## 9. Tables

Repeated attributes across named items become rows and columns.

**Input**

```text
Compare these plans by price and storage. Basic costs five dollars and has ten gigabytes. Pro costs twelve dollars and has one hundred gigabytes.
```

**Actual Polish output**

> Compare these plans by price and storage.
>
> | Name | price | storage |
> | --- | --- | --- |
> | Basic | costs five dollars | has ten gigabytes |
> | Pro | costs twelve dollars | has one hundred gigabytes |

<a id="blog"></a>
## 10. Blog posts

A personal article is grouped into paragraphs; no extra story is generated.

**Input**

```text
For an article on my personal website about learning to cycle to work. My first ride on Monday took twenty minutes. On Thursday I tried the park route and avoided the traffic. I got wet on Friday because I forgot my jacket. I plan to ride twice next week if it stays dry.
```

**Actual Polish output**

> For an article on my personal website about learning to cycle to work.
>
> My first ride on Monday took twenty minutes. On Thursday I tried the park route and avoided the traffic.
>
> I got wet on Friday because I forgot my jacket. I plan to ride twice next week if it stays dry.

<a id="essay"></a>
## 11. Essays

The argument and supporting ideas are grouped into paragraphs.

**Input**

```text
For my class I argue that libraries should stay open later. Students who work during the day need somewhere quiet in the evening. Longer hours might help, but staffing costs need review. A trial on Thursdays for one month would give us evidence before deciding.
```

**Actual Polish output**

> For my class I argue that libraries should stay open later. Students who work during the day need somewhere quiet in the evening.
>
> Longer hours might help, but staffing costs need review. A trial on Thursdays for one month would give us evidence before deciding.

<a id="notes"></a>
## 12. Meeting notes

Decisions, actions and unresolved details are separated for scanning.

**Input**

```text
We met this morning to discuss the export error. We agreed to keep the current release date for now. Maya will investigate the large file on Friday and Lee will check the logs. We have not agreed on a fix yet.
```

**Actual Polish output**

> We met this morning to discuss the export error. We agreed to keep the current release date for now.
>
> Maya will investigate the large file on Friday and Lee will check the logs. We have not agreed on a fix yet.

<a id="checklist"></a>
## 13. Checklists

A request for boxes to tick creates unchecked tasks.

**Input**

```text
Before the trip I want boxes to tick off for packing my passport, charging the camera and checking the train time before Friday. Don't buy another ticket until Maya confirms.
```

**Actual Polish output**

> Before the trip I want boxes to tick off for:
>
> - [ ] Packing my passport
> - [ ] charging the camera
> - [ ] checking the train time before Friday
> Don't buy another ticket until Maya confirms.

<a id="outline"></a>
## 14. Outlines

Main topics and their supplied subpoints form a nested list.

**Input**

```text
For my talk the main topics are privacy, speed and limitations. Under privacy include offline recording and local history. Under speed include loaded models and short recordings. Under limitations include transcription errors.
```

**Actual Polish output**

> For my talk the main topics are privacy, speed and limitations.
>
> - Privacy
>   - Offline recording
>   - Local history
> - Speed
>   - Loaded models
>   - Short recordings
> - Limitations
>   - Transcription errors

<a id="proposal"></a>
## 15. Proposals

The suggestion, reason, scope and approval request are grouped into paragraphs.

**Input**

```text
I suggest moving the printer to the spare room because it interrupts calls. We could try this for two weeks using the existing equipment. No new purchases are needed. Please approve the trial after Lee checks the room on Monday.
```

**Actual Polish output**

> I suggest moving the printer to the spare room because it interrupts calls.
>
> We could try this for two weeks using the existing equipment. No new purchases are needed. Please approve the trial after Lee checks the room on Monday.

<a id="agenda"></a>
## 16. Agendas

Meeting topics follow the stated order, keeping owners and the time limit.

**Input**

```text
At tomorrow's meeting first discuss the export error with Maya, then review the design with Lee, and finally decide whether to run another test. Keep it under thirty minutes.
```

**Actual Polish output**

> At tomorrow's meeting first:
>
> 1. Discuss the export error with Maya
> 2. Then review the design with Lee
> 3. Finally decide whether to run another test
> Keep it under thirty minutes.

<a id="faq"></a>
## 17. Questions and answers / FAQs

Multiple supplied question-and-answer pairs are separated; missing question marks are added.

**Input**

```text
All right, question one. is what color is the sky. The answer is blue. Question two is what color is the ocean? The answer is also blue. Yeah.
```

**Actual Polish output**

> What color is the sky?\
> Blue
>
> What color is the ocean?\
> Blue

<a id="social"></a>
## 18. Social posts

A short caption stays compact; no hashtags or links are invented.

**Input**

```text
For my Instagram caption: We are testing later library hours on Thursdays this month. Visit between six and eight and tell us how it works for you. This is a trial and permanent hours have not been decided.
```

**Actual Polish output**

> For my Instagram caption:\
> We are testing later library hours on Thursdays this month.\
> Visit between six and eight and tell us how it works for you.\
> This is a trial, and permanent hours have not been decided.

<a id="script"></a>
## 19. Scripts and dialogue

Each supplied speaker turn starts on its own line.

**Input**

```text
Maya says: The export failed again. Lee replies: Was it the large test file? Maya says: Yes, the small file worked. Lee replies: Let's check the logs before changing the release date.
```

**Actual Polish output**

> Maya says: The export failed again.\
> Lee replies: Was it the large test file?\
> Maya says: Yes, the small file worked.\
> Lee replies: Let's check the logs before changing the release date.

## If your result differs

- **Too many bullets:** ordinary opinions, questions and stories should stay prose. That can be a routing failure; naming the purpose can help but is not required.
- **A paragraph instead of structure:** confirm Polish is selected. A useful table needs supplied values; an FAQ needs supplied answers; an outline needs topics and subpoints.
- **Wrong information:** check the original transcript first. Retain the original and modified versions when reporting an editing failure.

See the [test examples and assertions](../windows/validation/QualityChecks.cs), [19 baseline inputs](../windows/validation/PolishChecks.cs), [layout planner](../windows/src/PolishLayout.cs) and [verification record](verification.json).
