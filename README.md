# SKPersona

This project is a demonstration of how to give Language Models (LLMs) a unique persona using the logit bias feature. It is built using [C#](https://dotnet.microsoft.com/en-us/languages/csharp), [Semantic Kernel](https://github.com/microsoft/semantic-kernel) and [Catalyst](https://github.com/curiosity-ai/catalyst).

> [!NOTE]
> The below examples were run using `gpt-3.5-turbo-0613` with `temperature=0` and varying `logit_bias`. LLMs are nondeterministic by nature, so an exact reproduction may be difficult.

> [!WARNING]
> This project is for demonstration purposes only and is not intended to be used in production environments.

## Logit Bias

### Overview

The [logit bias](https://help.openai.com/en/articles/5247780-using-logit-bias-to-define-token-probability) feature is a powerful parameter that allows us to manipulate the output of our language model. By adjusting the bias, we can make certain outputs more or less likely, effectively giving our model a 'persona'.

For example, if we want our model to prefer shorter sentences, we can increase the bias for common punctuation:

```text
User: Describe a rainbow.
Assistant: A rainbow is a beautiful and natural phenomenon. It appears as a circular arc of colors in the sky. It is formed when sunlight is refracted, or bent, as it passes through raindrops. The sunlight is then reflected inside the raindrop and refracted again. This process causes the light to separate into its component colors. The colors of a rainbow, from top to bottom, are red, orange, yellow, green, blue, indigo, and violet. The colors are vibrant and distinct. The rainbow usually appears after rain showers when the sun is still shining. It can also be seen near waterfalls or fountains. The sight of a rainbow is often associated with joy, hope, and wonder. It is a mesmerizing display of nature's beauty.
```

Conversely, if we decrease the bias:

```text
User: Describe a rainbow.
Assistant: A rainbow is a beautiful and natural phenomenon that occurs when sunlight is refracted, or bent, by water droplets in the air, creating a spectrum of colors in the sky.

Typically, a rainbow appears as a semi-circular arc of vibrant colors, with red being the outermost color and violet being the innermost color, although sometimes a full circle can be seen in certain conditions.

The colors of a rainbow, in order, are red, orange, yellow, green, blue, indigo, and violet, often remembered by the acronym ROYGBIV.

Each color of the rainbow is distinct and blends seamlessly into the next, creating a stunning display of hues that can be seen against a backdrop of dark clouds or a clear blue sky.

Rainbows are often seen after rain showers when the sun emerges from behind the clouds, casting its rays onto the raindrops in the air, causing them to act as tiny prisms that refract the sunlight and create the colorful spectrum.

The sight of a rainbow is often associated with feelings of joy, wonder, and hope, as it is a symbol of beauty and harmony in nature.

Rainbows are not physical objects that can be touched or approached, but rather optical illusions that appear to be located at a specific distance from the observer, making them seem elusive and magical.

Overall, a rainbow is a breathtaking and ephemeral display of colors that captivates the imagination and reminds us of the wonders of the natural world around us.
```

While both responses are semantically similar they read very differently. The first is concise and to the point while the second prefers newlines and a longer sentence structure.

### Personas

There are four persona examples this demo provides:

- `Base` no logit bias, default response
- `Punctuation` modifies the logit bias for common punctuation (used to generate the above examples)
- `Random` uses predetermined part of speech values (from `parts-of-speech.csv`) to generate logit bias values
- `Trained` uses Catalyst for [part of speech tagging](https://en.wikipedia.org/wiki/Part-of-speech_tagging) to extract parts of speech from `persona.txt` (default is the first three chapters of Alice in Windoerland) and then calculates logit bias values according to the probability of the part of speech appearing (versus in a production environment where these would be ideally calculated against a larger corpus of text)

## Running Locally

1. Clone the repository
   ```sh
   git clone https://github.com/anthonypuppo/skpersona.git
   ```
2. Install .NET packages
   ```sh
   dotnet restore
   ```
3. Open `appsettings.json`
   - Update the `General` section:
     - Persona may be one of `Base`, `Punctuation`, `Random`, or `Trained`
   - Update the `OpenAI` section:
     - Set your OpenAI key by opening a terminal in the project directory and using `dotnet user-secrets`
       ```bash
       dotnet user-secrets set "OpenAI:Key" "OPENAI_KEY"
       ```
4. Run the project
   ```sh
   dotnet run
   ```

## License

Copyright (c) Anthony Puppo. All rights reserved.

Licensed under the [MIT](LICENSE) license.
