# Human Confidence example

Human Confidence says how likely it is that a person is looking at a web
page. The project in this folder is a small web server and one page that
show it working, the way a publisher's website would use it. It's a web
demo rather than a console program, because the browser tests only run in a
browser.

| Example | Description |
| --- | --- |
| `HumanConfidenceWeb` | A demo web server that serves `page.html`, asks the cloud for the passive level, and redeems the sealed active level server side. ASP.NET Core minimal API, with no 51Degrees package needed. |

## The flow

The page shows three rows, one for each step.

1. **Passive.** The server asks the cloud itself, sending the visitor's
   request headers and IP address. `HumanConfidenceSource` is `Passive` and
   `HumanConfidence` is a plain level, `High`, `Medium`, or `Low`, worked
   out from the request alone. On a first visit it's `Medium` at most,
   because nothing has run in the browser yet. The server asks for it
   rather than reading it from the page, because a level sent by a browser
   can be forged.
2. **Active.** The page loads the 51Degrees client script. It runs the
   browser tests, which take a few seconds, and sends the answers to the
   cloud. When the script completes, `HumanConfidenceSource` is `Active` and
   `HumanConfidence` holds a sealed value. The browser can't read it or
   change it.
3. **Redeem.** The page hands the sealed value to the server, which sends
   it to the cloud's `hc/redeem` with the account's License Key and gets the
   true level back. A sealed value redeems once, within ten seconds of the
   tests. The page then offers to redeem the same value again, which
   answers `replayed`.

This is the same two-step pattern as the 51Did creator context check in
`../Did`: the browser carries a result it can't read, and your server
redeems it.

## What you need

- **A Resource Key** for an account on the Pro or Bespoke plan (CloudV5Pro
  or CloudV5Bespoke), with the properties `HumanConfidence` and
  `HumanConfidenceSource`. The browser tests script comes with
  `HumanConfidence` by itself, so you don't select it.
- **Leave the Resource Key's domain list empty,** or include `localhost`
  and the server's own address. The server calls the cloud with the same
  key, and a call from a server has no `Origin` header, so a key limited to
  domains is refused.
- **A License Key** of the same account. The server uses it to redeem. It
  never goes in the page.

## Environment variables

| Variable | Meaning |
| --- | --- |
| `_51DEGREES_RESOURCE_KEY` | The Resource Key. Required. |
| `_51DEGREES_LICENSE_KEY` | A License Key of the same account. Server side only. Needed to redeem. |
| `_51DEGREES_CLOUD_ENDPOINT` | Optional. The API base including the `/api/v4/` segment. It defaults to `https://cloud.51degrees.com/api/v4/`. Set it to `https://cloud-staging.51degrees.com/api/v4/` for staging. |
| `PORT` | The port to listen on. It defaults to `5110`. |

## How to run

From `HumanConfidenceWeb`, in PowerShell:

```
$env:_51DEGREES_RESOURCE_KEY = "your Resource Key"
$env:_51DEGREES_LICENSE_KEY = "your License Key"
dotnet run
```

Or in a bash shell:

```
export _51DEGREES_RESOURCE_KEY="your Resource Key"
export _51DEGREES_LICENSE_KEY="your License Key"
dotnet run
```

Then open `http://localhost:5110/` in a browser. To use staging, also set
`_51DEGREES_CLOUD_ENDPOINT` to `https://cloud-staging.51degrees.com/api/v4/`
before `dotnet run`.

## What you should see

In a current Chrome or Edge on a genuine computer or phone:

1. The first row shows `Passive` and a level, `Medium` at most.
2. After a few seconds, the second row shows `Active` and a sealed value.
3. The third row shows the level the server redeemed, usually `High`.
4. **Redeem the same value again** shows `replayed`.

If the second row stays `Passive`, the browser tests didn't run. The cloud
you're using may not have the tests yet, or the Resource Key may not
include `HumanConfidenceSource`.

## What changes the result

- **The browser.** Current Chrome and Edge on a genuine device reach `High`.
  Firefox often gets `Medium`.
- **TLS inspection.** Some antivirus software and corporate networks
  inspect TLS and replace the browser's own connection details. That stops
  `High`, so expect `Medium` from such a computer.
- **Headless and automated browsers** get `Low`.
- **Waiting.** A sealed value redeemed more than ten seconds after the tests
  answers `expired`.

## What a run costs

Every call to the cloud is one use against the subscription behind the
keys. A page load makes several: the server's passive call, the client
script's calls, and the redeem.
