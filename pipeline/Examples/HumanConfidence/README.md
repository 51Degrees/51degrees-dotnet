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

The page shows three rows, one for each step. At every step the cloud
gives the level and the demo reads it. The demo passes the evidence on as
it is and never works a level out itself.

1. **Passive.** The server asks the cloud itself, sending the visitor's
   request headers and IP address. `HumanConfidenceSource` is `Passive` and
   `HumanConfidence` is a plain level, `High`, `Medium`, or `Low`, for the
   request alone. On a first visit it's `Medium` at most, because nothing
   has run in the browser yet. The server asks for it rather than reading
   it from the page, because a level sent by a browser can be forged.
2. **Active.** The page loads the 51Degrees client script. It runs the
   browser tests, which take a few seconds, and sends the answers to the
   cloud. When the script completes, `HumanConfidenceSource` is `Active` and
   `HumanConfidence` holds a sealed value. The browser can't read it or
   change it.
3. **Redeem.** The page hands the sealed result to the server, which sends
   it to the cloud's `hc/redeem` with the account's License Key and gets the
   true Value back. The server sends it as `sealedResult`, and again as
   `value`, the name earlier cloud releases read, so the example works
   against either. The answer's `status` says what happened: `success`,
   `expired`, `replayed`, `unconfirmed` or `unreadable`. Only `success`
   carries the true Value in `humanConfidence`. A sealed result redeems
   once, within ten seconds of the tests. A later redeem answers `expired`.
   The page then offers to redeem the same sealed result again, which
   answers `replayed`.

This is the same two-step pattern as the 51Did creator context check in
`../Did`: the browser carries a result it can't read, and your server
redeems it.

## What you need

- **A Resource Key** for an account on the Pro or Bespoke plan (CloudV5Pro
  or CloudV5Bespoke), with the properties `HumanConfidence` and
  `HumanConfidenceSource`. The browser tests script comes with
  `HumanConfidence` by itself, so you don't select it. The server checks
  the key with the cloud's `accessibleproperties` when it starts. A key
  that doesn't carry `HumanConfidence` gets no level, so the page says so
  and links to [contact us](https://51degrees.com/contact-us?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-humanconfidence-readme.md&utm_term=human-confidence-not-in-key)
  rather than asking the cloud for what the key doesn't carry.
- **Leave the Resource Key's domain list empty,** or include `localhost`
  and the server's own address. The server calls the cloud with the same
  key, and a call from a server has no `Origin` header, so a key limited to
  domains is refused.
- **A License Key** of the same account. The server uses it to redeem. It
  never goes in the page.

## Environment variables

| Variable | Meaning |
| --- | --- |
| `_51DEGREES_RESOURCE_KEY` | The Resource Key. Required, and it has to carry `HumanConfidence`. |
| `_51DEGREES_LICENSE_KEY` | A License Key of the same account. Server side only. Needed to redeem. |
| `_51DEGREES_CLOUD_ENDPOINT` | Optional. The API base including the `/api/v4/` segment. It defaults to `https://cloud.51degrees.com/api/v4/`. |
| `_51DEGREES_EXAMPLE_HTTP_PORTS` | The ports to listen on, comma separated. It defaults to `5151`. |

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

Then open `http://localhost:5151/` in a browser. To use another copy of the
cloud service, also set `_51DEGREES_CLOUD_ENDPOINT` to its API base before
`dotnet run`.

## What you should see

In a current browser:

1. The first row shows `Passive` and a level, `Medium` at most.
2. After a few seconds, the second row shows `Active` and a sealed value.
3. The third row shows the level the server redeemed.
4. **Redeem the same value again** shows `replayed`.

If the second row stays `Passive`, the browser tests didn't run. The cloud
you're using may not have the tests yet, or the Resource Key may not
include `HumanConfidenceSource`.

If the page says the Resource Key doesn't carry Human Confidence, the
cloud lists no such property for the key. The page links to contact us,
where 51Degrees can add it to the account.

## What a run costs

Every call to the cloud is one use against the subscription behind the
keys. A page load makes several: the server's passive call, the client
script's calls, and the redeem. The server's check of the key at start-up
is one more, made once.

## Tests

`../../Tests/HumanConfidence.Examples.Tests` runs the example with no
Resource Key, reads these files for the rules every Human Confidence web
example keeps, and, with a key in `_51DEGREES_RESOURCE_KEY_BESPOKE`, asks the
live cloud whether the key carries `HumanConfidence`.

The tests of the passive level, and of a key outside the Bespoke product
getting a null source with a reason, need a cloud that serves
`HumanConfidenceSource`, which the production cloud does not yet. They call
the staging cloud in `CLOUD_STAGING_ENDPOINT` with the key in
`RESOURCE_KEY_BESPOKE_STAGING` and `_51DEGREES_RESOURCE_KEY_BASIC`. Only CI
holds the first two names, and
`.github/workflows/cloud-human-confidence.yml` runs these tests. Without a
name it needs, a live test skips and names it.
