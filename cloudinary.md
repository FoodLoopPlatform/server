> ## Documentation Index
> This page is part of the Image and Video APIs product. Fetch the complete documentation index for Image and Video APIs at: https://cloudinary.com/documentation/llms-image-and-video-apis.txt?referrer=docpage and then use it to discover all relevant pages before exploring further.
> If your task extends beyond this product, fetch the top-level index covering all Cloudinary products and topics at: https://cloudinary.com/documentation/llms.txt?referrer=docpage

# Cloudinary AI agent tools and MCP servers


> **TIP**: New to Cloudinary? Use the [Get Started prompt](ai_powerstart) on the AI Power Start page to get your project set up automatically. Your AI coding assistant installs the SDK, configures skills and MCP servers, and validates your setup in one step.

Cloudinary provides tools that enable AI agents to apply Cloudinary operations in your product environment and generate code that integrates Cloudinary functionality into your applications. Agents can configure environments, upload assets, manage assets and metadata, apply transformations, perform analysis, and more.

Tools include: 

* [Cloudinary Skills](#cloudinary_skills): Installable agent skills that steer LLMs toward correct patterns when answering Cloudinary questions or writing integration code.
* [MCP servers](#mcp_servers): Remote and local endpoints that expose Cloudinary capabilities to AI agents for performing operations and generating code.
* [LLM-friendly docs](#llm_friendly_docs): Structured references and tools that help language models generate accurate Cloudinary code and workflows.
* [Base44 integration](#base44_integration): A pre-built integration that you can add when creating apps using Base44 that enables you to incorporate Cloudinary functionality in your no-code apps.

> **NOTE**: If you don't yet have a Cloudinary account, your agent can [provision a Claimable Cloud for you](ai_agents_get_started) with a single command (`npx @cloudinary/cloud`) and start working right away. The Claimable Cloud is temporary: your agent gives you a claim URL where you enter your email and review the terms, then you confirm from a verification email to claim the cloud as your permanent account. It expires automatically if you don't.

## Cloudinary Skills
The [Cloudinary Skills pack](https://github.com/cloudinary-devs/skills) includes several use-case and framework specific skills as well as a general documentation lookup skill. These skills help AI agents answer Cloudinary questions and write integration code that follows proven patterns and best practices. With these skills, you can cut down on incorrect imports, invalid transformation syntax, and guesswork around things like implementing widgets, signing, and delivery URLs.

We recommend installing the `cloudinary-docs` skill and all other skills relevant to your requirements as part of your initial Cloudinary onboarding and setup to help your AI agent follow best practices, suggest the right features for your use case, and reduce implementation errors right from the start.

> **TIP**:
>
> :title=Tips

> * Cloudinary [MCP servers](#mcp_servers) complement the Cloudinary Skills. Used together, skills and MCP servers improve the end-to-end workflow. For example, our skills spell out best practices such as using named transformations to apply the same transformation across many assets. Based on that, the model can decide which MCP tools to run to create named transformations that match your needs.

> * We're regularly adding new skills and improving existing ones.  To check whether new skills have been added and whether you're using the latest versions of the skills you've already installed, compare the `version` value in the metadata of each of your `SKILL.md` files against those in the [Cloudinary Skills repo](https://github.com/cloudinary-devs/skills).


### 1. Install the skills

Run: 

```bash
npx skills add cloudinary-devs/skills
```

### 2. Select which skills to install

The skills installation command runs a CLI that enables you to install all skills or select which skills you want. You can install the skills to the active project or globally.

| Skill | What it does |
|-------|----------------|
| **cloudinary-docs** | Selects the most relevant markdown pages from the current documentation using the latest [llms.txt](https://cloudinary.com/llms.txt). Used when answering image or video management questions or integrating Cloudinary into your code. |
| **cloudinary-transformations** | Turns natural-language image and video transformation requirements into valid URL transformation strings that follow Cloudinary best practices. Used when building delivery URLs, applying image/video transformations, optimizing media, or debugging transformation syntax errors. |
| **cloudinary-react** | Provides opinionated React SDK patterns for configuration, common integration scenarios, and troubleshooting for frequent errors and TypeScript pitfalls. Used when developing with the Cloudinary React SDK. |
| **cloudinary-next** | Provides opinionated Next.js SDK patterns for Server and Client Component boundaries, server-side uploads and deletes, and troubleshooting for frequent errors and TypeScript pitfalls, using [next-cloudinary](nextjs_integration) components and helpers like `CldImage`, `CldVideoPlayer`, and `CldUploadWidget`. Used when developing Next.js apps with Cloudinary. |

> **TIP**:
>
> If you're using **Claude** or **Cursor**, you can install Cloudinary Skills and remote MCP servers from a single marketplace plugin installation:

> * **Claude:** [Cloudinary plugin for Claude](https://claude.com/plugins/cloudinary)

> * **Cursor:** [Cloudinary plugin for Cursor](https://cursor.com/marketplace/cloudinary)
> Note that these marketplace plugins include only selected skills from the Skills pack and may not be updated as frequently as the Skills pack. To choose from the latest and complete selection of available Cloudinary Skills, we recommend using the `npx skills` [CLI installation](#1_install_the_skills) and then separately [configure the Cloudinary MCP servers](#mcp_servers_installation) you want as described on this page.

### 3. Try out the skills

After installing the skills, you can use prompts like these to test them out.

#### Skill: cloudinary-docs

**Try this prompt:** 

`How do I sign a Cloudinary delivery URL to make it secure?`
&nbsp;

**What success looks like:** 

The doc skill is invoked and returns a detailed, implementation-focused answer grounded in the latest Cloudinary documentation, with working code examples.

---

#### Skill: cloudinary-transformations

**Try this prompt:** 

`Write me a Cloudinary transformation URL that resizes an image to 800px wide, uses face-aware cropping, and optimizes delivery.`
&nbsp;

**What success looks like:** 

The transformation skill is invoked and generates a valid transformation URL that follows transformation best practices (e.g. adds the `f_auto/q_auto` optimization actions as two separate components at the end of the transformation), for example:

```
https://res.cloudinary.com/YOUR_CLOUD_NAME/image/upload/c_fill,g_face,w_800/f_auto/q_auto/YOUR_IMAGE.jpg
```
---

#### Skill: cloudinary-react

**Try this prompt:** 

`Show me how to set up Cloudinary in a React app and display an optimized image using the Cloudinary React SDK.`
&nbsp;

**What success looks like:** 

The React skill is invoked and recommends current SDK packages and patterns rather than deprecated ones.

> **NOTES**:
>
> * Please share your feedback on existing skills and suggestions for new ones via the [GitHub repository](https://github.com/cloudinary-devs/skills/issues).

> * We'll continue to improve existing skills and add new skills to the Cloudinary Skills pack over time. Follow the [Image and Video Release Notes](programmable_media_release_notes) for updates.

## MCP servers

The Cloudinary MCP servers enable you to upload, manage, transform, and analyze your media assets as well as configure your product environment and create structured metadata or use MediaFlows.

### Available MCP servers

| MCP Server | Description | Add to Cursor |
|------------|-------------|-------------|
| **Asset Management** |  Upload and manage images, videos, and raw files, with support for advanced search and filtering. Easily delete or rename assets and take advantage of folders and tags for better organization. Includes dedicated transformation tools that use the [transformation rules file](#cloudinary_transformation_rules) to generate accurate transformation URLs and create derived assets. |   |
| **Environment Config** |  Manage product environment entities including upload presets, upload mappings, named transformations, webhook notifications, and streaming profiles. |  |
| **Structured Metadata** |  Define and manage structured metadata fields, values, and conditional metadata rules. |  |
| **Analysis** |  Leverage AI-powered content analysis for automatic tagging, along with tools for content moderation, safety checks, object detection, recognition, and more. |   |
| **MediaFlows** |  Create and manage automations in MediaFlows to automate media processing and delivery. For details, refer to the [MediaFlows MCP server documentation](mediaflows_mcp). |  |

> **TIP**: The [Cloudinary VS Code Extension (Beta)](https://marketplace.visualstudio.com/items?itemName=cloudinary.cloudinary) works great in tandem with our MCP servers! Use the extension to visually browse, search, and upload assets while using MCP servers for programmatic operations and AI-assisted development.


### MCP servers installation

You can install the Cloudinary MCP servers as remote servers (recommended) or local servers.

* **[Remote servers](cloudinary_llm_mcp#remote_mcp_servers)** are easier to set up and support OAuth authentication by default, with the option to [provide API credentials](#optional_authenticating_remote_mcp_servers_with_api_keys) instead if needed.
* **[Local servers](cloudinary_llm_mcp#local_mcp_servers)** run on your machine and require manual credential configuration, but may be preferred in environments that require more control or customization.

#### Remote MCP servers

Cloudinary hosts remote MCP servers that use OAuth authentication. They're easier to set up and maintain than local servers, and work with any MCP-compatible client.

1. Add the remote MCP server configurations objects shown below into your tool's MCP config file (e.g., under the `mcpServers` object in Cursor, or the root object in Claude Desktop).  For more detailed guidance, see our [tool-specific instructions](#install_remote_servers_in_cursor). We recommend you add all the Cloudinary MCP servers for easy access, but disable the servers and/or tools you don't currently need to limit context usage, reduce errors and improve prompt targeting.
2. Trigger your tool's Login flow to open a web browser where you can authenticate via OAuth to connect to your Cloudinary account and select the product environment (cloud) you want to use.

```json
{
  "cloudinary-asset-mgmt": {
    "url": "https://asset-management.mcp.cloudinary.com/mcp"
  },
  "cloudinary-env-config": {
    "url": "https://environment-config.mcp.cloudinary.com/mcp"
  },
  "cloudinary-smd": {
    "url": "https://structured-metadata.mcp.cloudinary.com/mcp"
  },
  "cloudinary-analysis": {
    "url": "https://analysis.mcp.cloudinary.com/sse"
  }
}
```

> **NOTE**: The Cloudinary MCP remote servers now use the `/mcp` endpoint for authentication (Streamable HTTP, stateless). This is the recommended endpoint to use for new configurations.
  
Previous versions of the Cloudinary MCP remote servers used the `/sse` endpoint. This endpoint is now deprecated (for all but the `cloudinary-analysis` server) and will be removed in a future version.
  
The `/sse` endpoint also accepts POST requests as an alias for `/mcp`, so clients that send Streamable HTTP to `/sse` will also work. However, you should use `/mcp` for new configurations.

#### Installing remote MCP servers via marketplace plugins
If you're using **Claude** or **Cursor**, you can install Cloudinary Skills and remote MCP servers from a single marketplace plugin installation:

* **Claude:** [Cloudinary plugin for Claude](https://claude.com/plugins/cloudinary)
* **Cursor:** [Cloudinary plugin for Cursor](https://cursor.com/marketplace/cloudinary)

Note that these marketplace plugins include only selected skills from the Skills pack and may not be updated as frequently as the Skills pack. To choose from the latest and complete selection of available Cloudinary Skills, we recommend using the `npx skills` [CLI installation](#1_install_the_skills) and then separately [configure the Cloudinary MCP servers](#mcp_servers_installation) you want as described on this page.
##### Optional. Authenticating remote MCP servers with API keys

By default, remote MCP servers use OAuth authentication. However, you can also authenticate using API keys via headers for scenarios where OAuth isn't suitable or when you prefer direct credential management.

**Using CLOUDINARY_URL (Simplest)**

Use a single header with your full Cloudinary URL:

```json
{
  "cloudinary-asset-mgmt": {
    "url": "https://asset-management.mcp.cloudinary.com/mcp",
    "headers": {
      "cloudinary-url": "cloudinary://api_key:api_secret@cloud_name"
    }
  }
}
```

**Using individual headers**

Specify each credential separately:

```json
{
  "cloudinary-env-config": {
    "url": "https://environment-config.mcp.cloudinary.com/mcp",
    "headers": {
      "cloudinary-cloud-name": "cloud_name",
      "cloudinary-api-key": "api_key",
      "cloudinary-api-secret": "api_secret"
    }
  }
}
```

**Adding custom configurations**

Add optional headers to control server behavior (for example, region or which tools are available):

```json
{
  "cloudinary-smd": {
    "url": "https://structured-metadata.mcp.cloudinary.com/mcp",
    "headers": {
      "cloudinary-url": "cloudinary://api_key:api_secret@cloud_name",
      "cloudinary-region": "api-eu",
      "cloudinary-tools": "list-metadata-fields,get-metadata-field,create-metadata-field"
    }
  }
}
```

**Surfacing rate-limit and request ID headers in tool results**

To include API rate-limit headers and request IDs in each tool result, enable header embedding:

```json
{
  "cloudinary-asset-mgmt": {
    "url": "https://asset-management.mcp.cloudinary.com/mcp",
    "headers": {
      "cloudinary-url": "cloudinary://api_key:api_secret@cloud_name",
      "cloudinary-embed-headers": "true"
    }
  }
}
```

Each tool result includes a `_headers` field with rate-limit and request tracing info:

```json
{
  "_headers": {
    "x-featureratelimit-limit": "10000",
    "x-featureratelimit-remaining": "9998",
    "x-featureratelimit-reset": "Thu, 13 Feb 2026 00:00:00 GMT",
    "x-request-id": "bfeaccc60050594832508590a358a1a4"
  }
}
```

Replace `cloud_name`, `api_key`, and `api_secret` with your actual Cloudinary credentials from the [API Keys](https://console.cloudinary.com/app/settings/api-keys) page in the Console Settings.

> **NOTE**:
>
> API key authentication is not currently supported by the **Analysis** MCP server. Use OAuth authentication for the Analysis server.

#### Install remote servers in Cursor 1. Navigate to **Settings** > **Cursor Settings** > **Tools and Integrations** > **MCP Tools** > **New MCP Server**. This opens the **~/.cursor/mcp.json** file.
2. Add your server configuration to the file.
3. Choose your authentication method:
   - **OAuth (default)**: Click **Connect** next to the server in the list to authenticate with your Cloudinary account and select the product environment you want to use.
   - **API keys**: Add your credentials in headers as shown in the second example below.

Refer to the [Cursor documentation](https://docs.cursor.com/context/model-context-protocol) for additional guidance. 

**With OAuth authentication:**

```json
{
  "cloudinary-asset-mgmt": {
    "url": "https://asset-management.mcp.cloudinary.com/mcp"
  },
  "cloudinary-env-config": {
    "url": "https://environment-config.mcp.cloudinary.com/mcp"
  },
  "cloudinary-smd": {
    "url": "https://structured-metadata.mcp.cloudinary.com/mcp"
  },
  "cloudinary-analysis": {
    "url": "https://analysis.mcp.cloudinary.com/sse"
  }
}
```

**With API key authentication:**

```json
{
  "cloudinary-asset-mgmt": {
    "url": "https://asset-management.mcp.cloudinary.com/mcp",
    "headers": {
      "cloudinary-url": "cloudinary://api_key:api_secret@cloud_name"
    }
  },
  "cloudinary-env-config": {
    "url": "https://environment-config.mcp.cloudinary.com/mcp",
    "headers": {
      "cloudinary-cloud-name": "cloud_name",
      "cloudinary-api-key": "api_key",
      "cloudinary-api-secret": "api_secret"
    }
  },
  "cloudinary-smd": {
    "url": "https://structured-metadata.mcp.cloudinary.com/mcp",
    "headers": {
      "cloudinary-url": "cloudinary://api_key:api_secret@cloud_name"
    }
  }
}
```

> **NOTE**: The Analysis MCP server does not support API key authentication. Use OAuth authentication for Analysis.

#### Install remote servers in VSCode 1. Make sure you have access to GitHub Copilot. 
2. Add your MCP server configuration to your VSCode MCP config file (`mcp.json`). You can access this file by running the **MCP: Add Server** command from the Command Palette (Ctrl/Cmd+Shift+P).
3. Choose your authentication method:
   - **OAuth (default)**: Follow the VSCode prompts to authenticate with your Cloudinary account and select the product environment you want to use.
   - **API keys**: Add your credentials in headers as shown in the second example below.

Refer to the [VSCode documentation](https://code.visualstudio.com/docs/copilot/chat/mcp-servers) for additional guidance.

**With OAuth authentication:**

```json
{
  "cloudinary-asset-mgmt": {
    "type": "sse",
    "url": "https://asset-management.mcp.cloudinary.com/mcp"
  },
  "cloudinary-env-config": {
    "type": "sse",
    "url": "https://environment-config.mcp.cloudinary.com/mcp"
  },
  "cloudinary-smd": {
    "type": "sse",
    "url": "https://structured-metadata.mcp.cloudinary.com/mcp"
  },
  "cloudinary-analysis": {
    "type": "sse",
    "url": "https://analysis.mcp.cloudinary.com/sse"
  }
}
```

**With API key authentication:**

```json
{
  "cloudinary-asset-mgmt": {
    "type": "sse",
    "url": "https://asset-management.mcp.cloudinary.com/mcp",
    "headers": {
      "cloudinary-url": "cloudinary://api_key:api_secret@cloud_name"
    }
  },
  "cloudinary-env-config": {
    "type": "sse",
    "url": "https://environment-config.mcp.cloudinary.com/mcp",
    "headers": {
      "cloudinary-cloud-name": "cloud_name",
      "cloudinary-api-key": "api_key",
      "cloudinary-api-secret": "api_secret"
    }
  },
  "cloudinary-smd": {
    "type": "sse",
    "url": "https://structured-metadata.mcp.cloudinary.com/mcp",
    "headers": {
      "cloudinary-url": "cloudinary://api_key:api_secret@cloud_name"
    }
  }
}
```

> **NOTE**: The Analysis MCP server does not support API key authentication. Use OAuth authentication for Analysis.

#### Install remote servers in Windsurf 1. Navigate to **Settings** > **Cascade Settings** > **MCP Servers** > **Edit Config**. This opens the **~/.codeium/windsurf/mcp_config.json** file.
2. Add your server configuration to the file.
3. Choose your authentication method:
   - **OAuth (default)**: Follow the Windsurf prompts to authenticate with your Cloudinary account and select the product environment you want to use.
   - **API keys**: Add your credentials in headers as shown in the second example below.

Refer to the [Windsurf documentation](https://docs.windsurf.com/windsurf/cascade/mcp) for additional guidance.

**With OAuth authentication:**

```json
{
  "cloudinary-asset-mgmt": {
    "serverUrl": "https://asset-management.mcp.cloudinary.com/mcp"
  },
  "cloudinary-env-config": {
    "serverUrl": "https://environment-config.mcp.cloudinary.com/mcp"
  },
  "cloudinary-smd": {
    "serverUrl": "https://structured-metadata.mcp.cloudinary.com/mcp"
  },
  "cloudinary-analysis": {
    "serverUrl": "https://analysis.mcp.cloudinary.com/sse"
  }
}
```

**With API key authentication:**

```json
{
  "cloudinary-asset-mgmt": {
    "serverUrl": "https://asset-management.mcp.cloudinary.com/mcp",
    "headers": {
      "cloudinary-url": "cloudinary://api_key:api_secret@cloud_name"
    }
  },
  "cloudinary-env-config": {
    "serverUrl": "https://environment-config.mcp.cloudinary.com/mcp",
    "headers": {
      "cloudinary-cloud-name": "cloud_name",
      "cloudinary-api-key": "api_key",
      "cloudinary-api-secret": "api_secret"
    }
  },
  "cloudinary-smd": {
    "serverUrl": "https://structured-metadata.mcp.cloudinary.com/mcp",
    "headers": {
      "cloudinary-url": "cloudinary://api_key:api_secret@cloud_name"
    }
  }
}
```

> **NOTE**: The Analysis MCP server does not support API key authentication. Use OAuth authentication for Analysis.

#### Install remote servers in Claude Code Choose your authentication method when adding the remote MCP servers:

**With OAuth authentication (default):**

1. Add the remote MCP servers using Claude Code's CLI commands shown below.
2. After adding the servers, use `/mcp` within Claude Code to authenticate with your Cloudinary account via OAuth.

```bash
# Add Asset Management server
claude mcp add --transport sse cloudinary-asset-mgmt https://asset-management.mcp.cloudinary.com/mcp

# Add Environment Config server  
claude mcp add --transport sse cloudinary-env-config https://environment-config.mcp.cloudinary.com/mcp

# Add Structured Metadata server
claude mcp add --transport sse cloudinary-smd https://structured-metadata.mcp.cloudinary.com/mcp

# Add Analysis server
claude mcp add --transport sse cloudinary-analysis https://analysis.mcp.cloudinary.com/sse
```

**With API key authentication:**

Add the servers with headers containing your Cloudinary credentials:

```bash
# Add Asset Management server with API key
claude mcp add --transport sse cloudinary-asset-mgmt https://asset-management.mcp.cloudinary.com/mcp \
  --header "cloudinary-url=cloudinary://api_key:api_secret@cloud_name"

# Add Environment Config server with individual headers
claude mcp add --transport sse cloudinary-env-config https://environment-config.mcp.cloudinary.com/mcp \
  --header "cloudinary-cloud-name=cloud_name" \
  --header "cloudinary-api-key=api_key" \
  --header "cloudinary-api-secret=api_secret"

# Add Structured Metadata server with API key
claude mcp add --transport sse cloudinary-smd https://structured-metadata.mcp.cloudinary.com/mcp \
  --header "cloudinary-url=cloudinary://api_key:api_secret@cloud_name"
```

> **NOTE**: The Analysis MCP server does not support API key authentication. Use OAuth authentication for Analysis.

Refer to the [Claude Code MCP documentation](https://docs.anthropic.com/en/docs/claude-code/mcp) for additional guidance.

#### Install remote servers in Claude Desktop There are three methods to add remote MCP servers in Claude Desktop:

**Method 1: Custom Connectors (Paid plans)**

1. Navigate to **Settings** > **Connectors**.
2. Locate the **Connectors** section. For organization accounts, you may need to toggle to **Organization connectors** at the top of the page.
3. Click **Add custom connector** at the bottom of the section.
4. Add your connector's remote MCP server URL (one at a time):
   - Asset Management: `https://asset-management.mcp.cloudinary.com/mcp`
   - Environment Config: `https://environment-config.mcp.cloudinary.com/mcp`
   - Structured Metadata: `https://structured-metadata.mcp.cloudinary.com/mcp`
   - Analysis: `https://analysis.mcp.cloudinary.com/sse`
5. Click **Add** to finish configuring each connector.

After adding the connectors, you can enable them via the **Search and tools** button in the lower left of your chat interface. For connectors that require authentication, click **Connect** to go through the OAuth authentication flow and grant permission for Claude to access your Cloudinary account and the product environment (cloud) you want to use.

**Method 2: Claude-Approved Directory (Asset Management only)**

The Cloudinary Asset Management MCP server is available as a Claude-approved tool in their official directory, making it easier to install:

1. Navigate to **Settings** > **Connectors**.
2. In the **Connectors** section, look for **Cloudinary Asset Management** in the available connectors list.
3. Click **Add** next to the Cloudinary Asset Management connector.
4. Follow the authentication flow to connect to your Cloudinary account and select the product environment (cloud) you want to use. 

> **NOTE**: This method is available only for the Asset Management MCP server. For the other Cloudinary MCP servers (Environment Config, Structured Metadata, and Analysis), use Method 1 or Method 3.

**Method 3: Configuration File (All plans including Free)**

1. Navigate to **Settings** > **Developer** > **Edit Config**. This opens the **claude_desktop_config.json** file.
2. Add your server configuration to the file.
3. Choose your authentication method:
   - **OAuth (default)**: Use the configuration shown in the first example below. After restarting, follow the Claude Desktop prompts to authenticate with your Cloudinary account and select the product environment you want to use.
   - **API keys**: Use the configuration shown in the second example below with your credentials in environment variables.
4. Restart Claude Desktop for the changes to take effect.

For more details, refer to the [Claude Desktop custom connectors documentation](https://support.anthropic.com/en/articles/11175166-getting-started-with-custom-connectors-using-remote-mcp) and the [Claude Desktop MCP documentation](https://modelcontextprotocol.io/quickstart/user).

**With OAuth authentication:**

```json
{
  "cloudinary-asset-mgmt": {
    "command": "npx",
    "args": [
      "mcp-remote",
      "https://asset-management.mcp.cloudinary.com/mcp"
    ]
  },
  "cloudinary-env-config": {
    "command": "npx",
    "args": [
      "mcp-remote",
      "https://environment-config.mcp.cloudinary.com/mcp"
    ]
  },
  "cloudinary-smd": {
    "command": "npx",
    "args": [
      "mcp-remote",
      "https://structured-metadata.mcp.cloudinary.com/mcp"
    ]
  },
  "cloudinary-analysis": {
    "command": "npx",
    "args": [
      "mcp-remote",
      "https://analysis.mcp.cloudinary.com/sse"
    ]
  }
}
```

**With API key authentication:**

```json
{
  "cloudinary-asset-mgmt": {
    "command": "npx",
    "args": [
      "mcp-remote",
      "https://asset-management.mcp.cloudinary.com/mcp"
    ],
    "env": {
      "MCP_HEADER_cloudinary-url": "cloudinary://api_key:api_secret@cloud_name"
    }
  },
  "cloudinary-env-config": {
    "command": "npx",
    "args": [
      "mcp-remote",
      "https://environment-config.mcp.cloudinary.com/mcp"
    ],
    "env": {
      "MCP_HEADER_cloudinary-cloud-name": "cloud_name",
      "MCP_HEADER_cloudinary-api-key": "api_key",
      "MCP_HEADER_cloudinary-api-secret": "api_secret"
    }
  },
  "cloudinary-smd": {
    "command": "npx",
    "args": [
      "mcp-remote",
      "https://structured-metadata.mcp.cloudinary.com/mcp"
    ],
    "env": {
      "MCP_HEADER_cloudinary-url": "cloudinary://api_key:api_secret@cloud_name"
    }
  }
}
```

> **NOTE**: The Analysis MCP server does not support API key authentication. Use OAuth authentication for Analysis.

#### Local MCP servers

Local MCP servers run on your machine using npm packages. You'll need to manage credentials and updates yourself.

Make sure you have **Node.js** (v18 or later) and **npm** installed before configuring them. 

1. Add the remote MCP server configurations objects shown below into your tool's MCP config file (e.g., under the `mcpServers` object in Cursor, or the root object in Claude Desktop).  For more detailed guidance, see our [tool-specific instructions](#install_remote_servers_in_cursor). We recommend you add all the Cloudinary MCP servers for easy access, but disable the servers and/or tools you don't currently need to limit context usage, reduce errors and improve prompt targeting.
2. Update your **cloud name**, **API key**, and **API secret** for each server you add.  You can find these credentials in the [Settings > API Keys](https://console.cloudinary.com/app/settings/api-keys) page in the Console. 

```json
"cloudinary-asset-mgmt": {
  "command": "npx",
  "args": ["-y", "--package", "@cloudinary/asset-management", "--", "mcp", "start"],
  "env": {
    "CLOUDINARY_CLOUD_NAME": "cloud_name",
    "CLOUDINARY_API_KEY": "api_key",
    "CLOUDINARY_API_SECRET": "api_secret"
  }
},
"cloudinary-env-config": {
  "command": "npx",
  "args": ["-y", "--package", "@cloudinary/environment-config", "--", "mcp", "start"],
  "env": {
    "CLOUDINARY_CLOUD_NAME": "cloud_name",
    "CLOUDINARY_API_KEY": "api_key",
    "CLOUDINARY_API_SECRET": "api_secret"
  }
},
"cloudinary-smd": {
  "command": "npx",
  "args": ["-y", "--package", "@cloudinary/structured-metadata", "--", "mcp", "start"],
  "env": {
    "CLOUDINARY_CLOUD_NAME": "cloud_name",
    "CLOUDINARY_API_KEY": "api_key",
    "CLOUDINARY_API_SECRET": "api_secret"
  }
},
"cloudinary-analysis": {
  "command": "npx",
  "args": ["-y", "--package", "@cloudinary/analysis", "--", "mcp", "start"],
  "env": {
    "CLOUDINARY_CLOUD_NAME": "cloud_name",
    "CLOUDINARY_API_KEY": "api_key",
    "CLOUDINARY_API_SECRET": "api_secret"
  }
}
```

#### Install local servers in Cursor 1. Navigate to **Settings** > **Cursor Settings** > **MCP Tools** > **New MCP Server**. This opens the **~/.cursor/mcp.json** file.
2. Add your server configuration to the file as shown below.
3. Update your **cloud name**, **API key**, and **API secret** in the configuration with your actual Cloudinary credentials.

Refer to the [Cursor documentation](https://docs.cursor.com/context/model-context-protocol) for additional guidance.
Below is the configuration for the `cloudinary-asset-mgmt` server. Apply the same configuration pattern to other servers by adding a copy of the server json object, but replacing `@cloudinary/asset-management` with the respective package names:

* `@cloudinary/environment-config`
* `@cloudinary/structured-metadata`
* `@cloudinary/analysis`

Make sure to update your **cloud name**, **API key**, and **API secret** for each server you add.  You can find these credentials in the [Settings > API Keys](https://console.cloudinary.com/app/settings/api-keys) page in the Console. 
```json
{
  "cloudinary-asset-mgmt": {
    "command": "npx",
    "args": ["-y", "--package", "@cloudinary/asset-management", "--", "mcp", "start"],
    "env": {
      "CLOUDINARY_CLOUD_NAME": "cloud_name",
      "CLOUDINARY_API_KEY": "api_key",
      "CLOUDINARY_API_SECRET": "api_secret"
    }
  }
}
```

#### Install local servers in VSCode 1. Make sure you have access to GitHub Copilot. 
2. Add your MCP server configuration to your VSCode MCP config file (`mcp.json`). You can access this file by running the **MCP: Add Server** command from the Command Palette (Ctrl/Cmd+Shift+P).
3. Update your **cloud name**, **API key**, and **API secret** in the configuration with your actual Cloudinary credentials.

Refer to the [VSCode documentation](https://code.visualstudio.com/docs/copilot/chat/mcp-servers) for additional guidance.
Below is the configuration for the `cloudinary-asset-mgmt` server. Apply the same configuration pattern to other servers by adding a copy of the server json object, but replacing `@cloudinary/asset-management` with the respective package names:

* `@cloudinary/environment-config`
* `@cloudinary/structured-metadata`
* `@cloudinary/analysis`

Make sure to update your **cloud name**, **API key**, and **API secret** for each server you add.  You can find these credentials in the [Settings > API Keys](https://console.cloudinary.com/app/settings/api-keys) page in the Console. 
```json
{
  "cloudinary-asset-mgmt": {
    "type": "stdio",
    "command": "npx",
    "args": ["-y", "--package", "@cloudinary/asset-management", "--", "mcp", "start"],
    "env": {
      "CLOUDINARY_CLOUD_NAME": "cloud_name",
      "CLOUDINARY_API_KEY": "api_key",
      "CLOUDINARY_API_SECRET": "api_secret"
    }
  }
}
``` 

#### Install local servers in Windsurf 1. Navigate to **Settings** > **Cascade Settings** > **MCP Servers** > **Edit Config**. This opens the **~/.codeium/windsurf/mcp_config.json** file.
2. Add your server configuration to the file as shown below.
3. Update your **cloud name**, **API key**, and **API secret** in the configuration with your actual Cloudinary credentials.

Refer to the [Windsurf documentation](https://docs.windsurf.com/windsurf/cascade/mcp) for additional guidance.
Below is the configuration for the `cloudinary-asset-mgmt` server. Apply the same configuration pattern to other servers by adding a copy of the server json object, but replacing `@cloudinary/asset-management` with the respective package names:

* `@cloudinary/environment-config`
* `@cloudinary/structured-metadata`
* `@cloudinary/analysis`

Make sure to update your **cloud name**, **API key**, and **API secret** for each server you add.  You can find these credentials in the [Settings > API Keys](https://console.cloudinary.com/app/settings/api-keys) page in the Console. 
```json
{
  "cloudinary-asset-mgmt": {
    "command": "npx",
    "args": ["-y", "--package", "@cloudinary/asset-management", "--", "mcp", "start"],
    "env": {
      "CLOUDINARY_CLOUD_NAME": "cloud_name",
      "CLOUDINARY_API_KEY": "api_key",
      "CLOUDINARY_API_SECRET": "api_secret"
    }
  }
}
```

#### Install local servers in Claude Code 1. Make sure you have **Node.js** (v18 or later) and **npm** installed.
2. Add the local MCP servers using Claude Code's CLI commands shown below.
3. Update your **cloud name**, **API key**, and **API secret** in the commands with your actual Cloudinary credentials.

Refer to the [Claude Code MCP documentation](https://docs.anthropic.com/en/docs/claude-code/mcp) for additional guidance.

```bash
# Add Asset Management server
claude mcp add cloudinary-asset-mgmt \
  --env CLOUDINARY_CLOUD_NAME=cloud_name \
  --env CLOUDINARY_API_KEY=api_key \
  --env CLOUDINARY_API_SECRET=api_secret \
  -- npx -y @cloudinary/asset-management mcp start

# Add Environment Config server
claude mcp add cloudinary-env-config \
  --env CLOUDINARY_CLOUD_NAME=cloud_name \
  --env CLOUDINARY_API_KEY=api_key \
  --env CLOUDINARY_API_SECRET=api_secret \
  -- npx -y @cloudinary/environment-config mcp start

# Add Structured Metadata server
claude mcp add cloudinary-smd \
  --env CLOUDINARY_CLOUD_NAME=cloud_name \
  --env CLOUDINARY_API_KEY=api_key \
  --env CLOUDINARY_API_SECRET=api_secret \
  -- npx -y @cloudinary/structured-metadata mcp start

# Add Analysis server
claude mcp add cloudinary-analysis \
  --env CLOUDINARY_CLOUD_NAME=cloud_name \
  --env CLOUDINARY_API_KEY=api_key \
  --env CLOUDINARY_API_SECRET=api_secret \
  -- npx -y @cloudinary/analysis mcp start
```

#### Install local servers in Claude Desktop 1. Navigate to **Settings** > **Developer** > **Edit Config**. This opens the **claude_desktop_config.json** file.
2. Add your server configuration to the file as shown below.
3. Update your **cloud name**, **API key**, and **API secret** in the configuration with your actual Cloudinary credentials.
4. Restart Claude Desktop for the changes to take effect.

Refer to the [Claude Desktop documentation](https://modelcontextprotocol.io/quickstart/user) for additional guidance.
Below is the configuration for the `cloudinary-asset-mgmt` server. Apply the same configuration pattern to other servers by adding a copy of the server json object, but replacing `@cloudinary/asset-management` with the respective package names:

* `@cloudinary/environment-config`
* `@cloudinary/structured-metadata`
* `@cloudinary/analysis`

Make sure to update your **cloud name**, **API key**, and **API secret** for each server you add.  You can find these credentials in the [Settings > API Keys](https://console.cloudinary.com/app/settings/api-keys) page in the Console. 
```json
{
  "cloudinary-asset-mgmt": {
    "command": "npx",
    "args": ["-y", "--package", "@cloudinary/asset-management", "--", "mcp", "start"],
    "env": {
      "CLOUDINARY_CLOUD_NAME": "cloud_name",
      "CLOUDINARY_API_KEY": "api_key",
      "CLOUDINARY_API_SECRET": "api_secret"
    }
  }
}
```

## LLM-friendly docs

Alongside Cloudinary's MCP servers, we also recommend that you take advantage of the following Cloudinary documentation resources to get the optimal results when coding with LLM clients.

### Cloudinary in Context7

[Context7](https://context7.com/) is a widely used MCP server for developer documentation code examples (with over 17,000 dev libraries indexed), including Cloudinary docs. It regularly pulls every code example for every SDK from the Cloudinary docs and makes it available to your LLM for reference. 

When you use Context7 as part of your Cloudinary-specific LLM prompts, you ensure that your LLM has up-to-date code examples for the latest Cloudinary features and that your LLM client generates more accurate and relevant code for your use-case.

**To use Context7 with Cloudinary:**

1. Ensure you've added the Context7 MCP to your MCP-supporting LLM client. For instructions, see the [Context7 MCP README](https://github.com/upstash/context7).
2. When you ask your LLM to write Cloudinary code, append `use context7` to the end of your prompt.
  
### Cloudinary transformation rules

[cloudinary_transformation_rules.md](https://cloudinary.com/documentation/cloudinary_transformation_rules.md) is a rules-based markdown file that helps LLMs generate syntactically correct, hallucination-free Cloudinary transformations.

Even with all the great content and code examples on the web, LLMs (and even the most experienced Cloudinary developers) sometimes struggle to write syntactically correct transformations, especially for more complex use cases. 

By adding this new transformation rules markdown file as documentation context for your transformation-related prompts, your LLM client generally produces more accurate transformations and only uses valid transformation parameters and options that are part of the official Cloudinary documentation.

**To use the cloudinary_transformation_rules.md file**:

When writing a prompt related to Cloudinary transformations, add the [cloudinary_transformation_rules.md](https://cloudinary.com/documentation/cloudinary_transformation_rules.md) file as a documentation context for your prompt.  

**See also**: [How to add context files as documentation context](#how_to_add_documentation_context_files)

### Cloudinary docs as context

The Cloudinary docs site is available in it's standard HTML format as well as in markdown format with an accompanying `llms.txt` file. 

Because the docs explain how to choose between similar features, give clarity on how to use features together to achieve use cases, and include important tips, troubleshooting, and guidelines for achieving best results, providing relevant Cloudinary documentation [as context](#adding_documentation_context_to_cursor) in addition to using [Context7](#cloudinary_in_context7) and the [transformation rules](#cloudinary_transformation_rules) file mentioned above is more likely to help the LLM model provide the right code or answers.

There are a few ways you can do this: 

* **[Add a specific markdown doc page](#doc_site_markdown_pages)**: If you know which docs page(s) cover the relevant information, you can directly provide those markdown pages as context for your request.
* **[Add the llms.txt files as context](#llms_txt)**: If you want the LLM to use the whole docs site as context, and the LLM client you're using correctly processes `llms.txt` files that point to markdown files, the llms.txt file is the most efficient way to use the entire Cloudinary documentation set as context.
* **[Add the HTML version of the docs site as context](#html_doc_site_as_context)**: If your LLM client doesn't yet process `llms.txt` files that point to markdown files, you can provide the HTML version of the Cloudinary docs site as context. While not as efficient as the above options, it provides the same overall benefits.

**See also**: [How to add context files as documentation context](#how_to_add_documentation_context_files)

#### Docs site markdown pages

In addition to the standard Cloudinary docs website, every Cloudinary doc page is also published as a clean, LLM-friendly markdown page.
These markdown pages enable LLM-based IDEs and chat clients to process and consume content more efficiently, using a minimum of tokens.  Thus, if you want your LLM client to build code or answer questions based on a specific documentation page (rather than its previously trained data or a general web search), you can provide it the relevant markdown page(s) as context.

You can easily open, copy, or download the markdown content from each doc page using the relevant buttons below the page heading.

**To use specific Cloudinary docs markdown pages**:

1. Retrieve the URL to the markdown page by clicking **Open as Markdown** from any Cloudinary doc page and copy the URL.
2. Specify the relevant markdown URL as context in your LLM-client. If you're working with a chat client that doesn't support remote URLs, download the markdown file using the **Download Markdown** button and upload it for context with your prompt.

#### llms.txt
The Cloudinary docs site includes an `llms.txt` file that structurally references all the docs site markdown files.

If your LLM client supports processing `llms.txt` files that point to markdown files, you can pass the [Cloudinary docs llms.txt](https://cloudinary.com/documentation/llms.txt) file as context for your Cloudinary-specific prompts.  

This enables the LLM client to choose the markdown pages it finds relevant to help the model form its answer.

> **NOTE**: `llms.txt` is a _proposed_ standard for helping LLMs identify and process website content. Different LLM tools support and/or process `llms.txt` files differently and the way they use it may change over time.  Check with your LLM tool documentation for information on whether or how to use `llms.txt` files. [Learn more about llms.txt](https://llmstxt.org/).

#### HTML docs site as context

In case your LLM client doesn't yet support processing `llms.txt` files that point to markdown files (doesn't automatically index all the files referenced from the llms.txt), you can add the entire `https://cloudinary.com/documentation` website as context and your tool can crawl the website from there.

### How to add documentation context files

When you add a document or set of documents as context for an LLM, the system parses and chunks the file, then stores it in a vector database. This enables the LLM to analyze your prompt, pull the most relevant chunked vectors, and address your question as if it had 'read' the document(s). 

Each LLM client has a different way to add a document as context. In some cases, it only stores your content in the vector database temporarily, and in other cases they're indexed into persistent stores for repeated use. 

Below are instructions for how to add a document as context in some commonly used LLM tools.

> **NOTE**: These instructions are accurate as of the publication of this page. However LLM tools are updating regularly and the process may change over time. Refer to your tool's documentation for most reliable instructions.

#### Add doc context in Cursor 1. Open your **Cursor Settings** and select **Indexing and Docs**.
2. In the Docs section, click **Add Doc**.
3. Paste the relevant documentation markdown or llms.txt URL.
4. In the form that opens:
   1. Update the document name to a descriptive name you'll recognize later (By default, Cursor gives just the file's domain name as the document name). For example, `cld-transformation-rules` or `cld-docs-llms`
   2. Set both **Prefix** and **Entrypoint** to `https://cloudinary.com/documentation/cloudinary_transformation_rules.md` (the prefix defaults to all of cloudinary.com/documentation).
5. When writing a prompt related to transformations, click the **Add context** (**@**) button and select the pre-defined name of the file you want to use for context.

For more details or the latest information, see [Cursor Docs Chat Context](https://docs.cursor.com/context/@-symbols/@-docs)
   
#### Add doc context in VSCode 1. Make sure you have access to GitHub Copilot. 
2. In your chat, do one of the following:
   
   * Drag and drop files or folders from the Explorer view, Search view, or editor tabs onto the Chat view to add them as context.
   * Use the Add Context button in the Chat view and select Files & Folders or Symbols.

For more details or the latest information, see [VSCode Copilot Chat Context](https://code.visualstudio.com/docs/copilot/chat/copilot-chat-context)

#### Add doc context in WindSurf 1. In the WindSurf sidebar, go to the **Context** tab.
2. In **Pinned Contexts**, paste the URL of the relevant documentation markdown or llms.txt URL.
3. Once added, WindSurf includes that document as live context in all future queries.

Alternatively, add the URL in the context of a specific chat using the **@Mention** option.

For more details or the latest information, see **@-Mentions** and **Persistent Context** in [WindSurf Chat Overview](https://docs.windsurf.com/chat/overview)

#### Add doc context in Claude Desktop 1. Download the relevant documentation markdown or llms.txt file.
2. In a new chat, attach the relevant file to upload it. 
3. In your prompt, clarify that you want it to use the uploaded file.  For example: 
   `Using the doc I just uploaded, what’s the syntax for chained transformations?`

> **TIP**: On Paid Claude plans, you can also add documents to your project knowledge base and use them across chats.

For more details or the latest information, see **Project Knowledge** in [Claude Project Management](https://support.anthropic.com/en/articles/9519177-how-can-i-create-and-manage-projects)

## Tips and considerations

* MCP servers and LLM tools in general aren't always consistent. A prompt or question you write today may yield different results tomorrow.
* Given the option, use the most advanced LLM model available. The better the LLM model, the better your results.
* If there are alternative ways to achieve an aim, but you want to use Cloudinary functionality, explicitly tell the LLM to use Cloudinary.
* In some MCP clients, you don't need to reference an MCP server directly in a prompt if you enable the server.
* When prompting for SDK-based code (e.g., Node, Python), include `use Context7` at the end of your prompt for more accurate, context-aware output (see [Cloudinary in Context7](#cloudinary_in_context7)).
* When asking for transformation logic, include **cloudinary_transformation_rules.md** as a document context (see [Cloudinary transformation rules](#cloudinary_transformation_rules)). The Asset Management MCP server includes dedicated transformation tools that automatically leverage this file to generate accurate transformation URLs and create derived assets.
* When asking the LLM to implement a broad use-case or to answer a question (vs requesting to implement a very specific feature or implement specific SDK code), provide the relevant Cloudinary docs site markdown page(s) or the entire Cloudinary docs site (via llms.txt or HTML URL) as context. (See [Cloudinary docs as context](#cloudinary_docs_as_context))
* Enable any required add-ons in the [Add-ons](https://console.cloudinary.com/app/settings/addons) section of Console Settings before referencing them in prompts.
* If the functionality you need requires activation of certain Console settings, such as allowing features that default to disabled in the [Security Settings](https://console.cloudinary.com/app/settings/security), make sure you enable those before asking the LLM to perform them. 
* Let the model know whether your cloud uses [dynamic or fixed folder mode](folder_modes).
* When managing metadata, mention specific fields or tagging strategies you'd like the tool to use or apply.

## Use cases and examples 

Here are some examples prompts that you can use for inspiration:

* "Upload assets from the 'summer_campaign' folder within the project. Auto-tag all the images using the **Google auto-tagging** add-on."
  > **NOTE**: Remember to first register for the add-on.
* "Implement a client-side upload in the app that allows users to upload user-generated content. Use Cloudinary's **WebPurify** add-on to moderate those assets."
* "Return the URLs of all assets that have the tag 'transparent'."
* "Add the value 'Expired' to the metadata field 'Status' for assets with SKUs '123789', '998y285', and '825168'."
* "Delete all assets tagged with 'campaign-2024'."
* "Generate a transformation URL for 'sample.jpg' that resizes it to 300x300, applies auto gravity, and converts to WebP format."
* "Create a derived image for 'product-photo.png' with a circular crop at 400x400 and add a text overlay that says 'New Arrival'."
* "Create a signed upload preset that allows image uploads, marks access control as 'restricted', and generates `eager` transformations to size the images for mobile devices."
* "Create a named transformation called 'secure' that places the asset with product ID 'my-company-logo' as a watermark in the center of all assets with 50% opacity."
  

## Base44 integration

[Base44](https://base44.com/) is an AI powered tool that let you build apps without coding.  Base44 offers a Cloudinary integration with preloaded knowledge of Cloudinary's features and direct access to MCP servers. Use it to quickly create full apps with natural language prompts. 

> **INFO**:
>
> The integration uses backend functionality, which is currently available only for users on the Base44 **Builder** or higher plan.

### Getting started

Here are the steps to set up and start using the Base44 Cloudinary integration:

1. Go to the Base44 [Cloudinary Integration](https://app.base44.com/integrations-catalog/item/684a7b524c8cb269f7b2640a) from the integrations catalog.
2. Click **Use this integration**.
3. Scroll down to fill in your **CLOUDINARY_CLOUD_NAME**, **CLOUDINARY_API_KEY**, and **CLOUDINARY_API_SECRET** in the corresponding fields. You can find these credentials on the [API Keys](https://console.cloudinary.com/app/settings/api-keys) page of the Console Settings.
4. Add your prompt and click on the submit button.

> **TIP**: In each request, make sure to ask it to use Cloudinary if you want Cloudinary functionality included.

### Video tutorial

Watch a short video to help you get started with Cloudinary and Base44. It includes setup instructions along with example apps to inspire your own no-code workflows.

  This video is brought to you by Cloudinary's video player - embed your own!Use the controls to set the playback speed, navigate to chapters of interest and select subtitles in your preferred language.
{videoTranscript:publicId=training/base44_intro}

### Supported Cloudinary functionalities

Using the Cloudinary Base44 integration, you can create apps that implement the following Cloudinary functionalities:

* Upload an image or video from the local directory or using a remote URL.
* Retrieve asset information.
* Update assets, including tags and metadata, and perform moderation.
* Retrieve assets from a specific folder.
* Search for assets.
* List folders.
* Retrieve metadata fields.
* Retrieve tags.
* Apply transformations to assets.
  
In addition, you can integrate the following widgets:

* Upload Widget
* Media Library Widget
* Cloudinary Video Player

> **INFO**: You can attempt to use other Cloudinary features in your Base44 apps via the integration, but they may not work as expected. Please contact our [support team](https://support.cloudinary.com/hc/en-us/requests/new) to let us know which additional functionalities you'd like us to support.

### Tips

* Enable any required add-ons in the [Add-ons](https://console.cloudinary.com/app/settings/addons) section of Console Settings before referencing them in your prompt.
* Be explicit that you want to use Cloudinary for a task, even if you've already mentioned it earlier. 
* If you want to use a Cloudinary widget, clearly specify this in your prompt and name the widget (e.g., Upload Widget, Media Library Widget).
* Some tasks such as moderating or auto-tagging videos require processing time and may not be available immediately after upload. Your app may need to either wait for a [webhook notification](notifications) or include a follow-up user action (e.g., clicking a refresh button) to display the updated data.
* To use an image or video overlay, the asset must already exist in your product environment. Reference the overlay asset in the prompt using its public ID.
* If errors come up during the generation process, click the prompt to let the AI attempt to resolve them.
* After Base44 generates your app, you can continue refining it by adding additional prompt details.

### Example prompts and apps

Below are example prompts and the apps they generated, to give you a sense of what's possible. Keep in mind that MCP servers may not always produce consistent results, and repeating the same prompt can yield different outcomes over time.

{table:class=no-borders overview} App | Description | Prompt
|---|-------------|--------|
| [LuxeFind](https://app--luxe-find-51efc892.base44.app) | Display accessory product images on a PDP. | Create an e-commerce app using Cloudinary. Show assets from the **Accessories** folder in a grid, along with their metadata: **Price**, **Description**, and **SKU** (external IDs: `price_id`, `description_id`, and `sku_id`). Add a preview icon that opens a Quick View modal. Use Cloudinary's resize and crop transformations to ensure the images fully fill the modal space. |
| [PixelCraft](https://app--pixel-craft-b4108f3e.base44.app) | Crop and resize an image uploaded to Cloudinary. | Develop a one-page image editing tool where users can upload an image to Cloudinary. Display the image in an editing area with controls for cropping and resizing using Cloudinary's image transformation options. Apply the selected transformations and provide an option to download the edited image. |
| [NewsHub](https://app--news-hub-52f78126.base44.app) | Share news stories with an image or video. | Create an app that lets users upload news stories with an image or video. Use the Cloudinary Upload Widget configured to accept both media types. Tag uploaded assets by the story name. Display all stories in a grid layout, including the new one. Use Cloudinary's crop and resize options to fit assets into their bounding boxes while keeping the focus on key content. |
| [CloudSearch & Mark](https://app--cloud-search-mark-25dcd33d.base44.app) | Apply watermarks to selected assets for third-party viewing. | Create an app that prompts the user for a search term. Search Cloudinary for assets with a matching public ID, display name, tag, or folder name. Display matching assets in a grid. Overlay the `cloudinary_logo` asset as a centered watermark with 40% opacity. Ensure consistent image sizing and even logo placement. **Note:** The `cloudinary_logo` watermark already exists in the Cloudinary product environment. |

## n8n integration

n8n is a powerful workflow automation platform that enables you to connect various apps and services through a visual, node-based interface. With built-in AI capabilities, n8n allows you to create intelligent workflows that leverage language models for tasks like content generation, data analysis, and automated decision-making.

You can install the **Cloudinary n8n node** and use it to upload assets and manage asset metadata. 

Learn about the [Cloudinary n8n integration](n8n_integration).