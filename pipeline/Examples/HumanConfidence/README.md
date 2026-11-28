# Human Confidence example

Human Confidence says how likely it is that a person is looking at a web
page. The project in this folder is a small web server and one page that
show it working, the way a publisher's website would use it. It's a web
demo rather than a console program, because the browser tests only run in a
browser.

| Example | Description |
| --- | --- |
| `HumanConfidenceWeb` | A demo web server that serves `page.html` and runs the 51Degrees Pipeline, which asks the cloud and redeems the sealed active level. The server only reads the two Properties. ASP.NET Core minimal API, with the `FiftyOne.Pipeline.Web` and `FiftyOne.Pipeline.CloudRequestEngine` packages. |

## The flow

The page shows three rows, one for each step. At every step the cloud
gives the level, the server's 51Degrees Pipeline asks for it, and the
server reads two Properties, `HumanConfidence` and
`HumanConfidenceSource`. The server never redeems a sealed value itself,
because the Pipeline does.

1. **Passive.** The page asks the server, whose Pipeline asks the cloud
   with the visitor's request. `HumanConfidenceSource` is `Passive` and
   `HumanConfidence` is a plain level, `High`, `Medium`, or `Low`, for the
   request alone. The server asks for it rather than reading it from the
   page, because a level sent by a browser can be forged.
2. **Active.** As the server serves the page, it asks its Pipeline for a
   page id, with `IssueHumanConfidencePage` on the Cloud Request Engine,
   and the Pipeline keeps a new transaction for that page. The page loads
   the 51Degrees client script from the server's Pipeline, at
   `/51Degrees.core.js?humanconfidencepage=<page id>`, so the script's
   address is the page's own and every call the script makes carries the
   page id. The Pipeline adds the page's transaction to each of those
   calls to the cloud. The script runs the browser tests, which take a
   few seconds, and sends the answers back through the Pipeline to the
   cloud. When the script completes, `HumanConfidenceSource` is `Active`
   and `HumanConfidence` holds a sealed value. The browser can't read it
   or change it.
3. **Redeemed.** The page sends the sealed value back to the server as
   `humanconfidence`, with its page id as `humanconfidencepage`. The
   Pipeline redeems it with that page's transaction and the account's
   License Key, through the same call to the cloud that asks for
   everything else, and the server reads `HumanConfidence` as the level. A
   sealed value redeems once, within ten seconds of the tests, and only
   for the page it was made for. Whenever it isn't redeemed, as for a page
   id the Pipeline doesn't know or one already used, `HumanConfidence` is
   `Medium`. The page then offers to send the same value again, which
   gives `Medium`.

This is the same two-step pattern as the 51Did creator context check in
`../Did`: the browser carries a result it can't read, and your server's
Pipeline redeems it.

## What you need

- **A Resource Key** for an account on the Pro or Bespoke plan (CloudV5Pro
  or CloudV5Bespoke), with the properties `HumanConfidence` and
  `HumanConfidenceSource`. The browser tests script comes with
  `HumanConfidence` by itself, so you don't select it. The Pipeline reads
  the key's properties from the cloud's `accessibleproperties` when it
  starts, and the server checks that list. A key
  that doesn't carry `HumanConfidence` gets no level, so the page says so
  and links to [contact us](https://51degrees.com/contact-us?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-humanconfidence-readme.md&utm_term=human-confidence-not-in-key)
  rather than asking the cloud for what the key doesn't carry.
- **Leave the Resource Key's domain list empty,** or include `localhost`
  and the server's own address. The server calls the cloud with the same
  key, and a call from a server has no `Origin` header, so a key limited to
  domains is refused.
- **A License Key** of the same account. The Pipeline uses it to redeem.
  It never goes in the page. Without one nothing is redeemed, and the
  third row shows `Medium`.

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
3. The third row shows `Active` and the level the Pipeline redeemed.
4. **Send the same sealed value again** shows `Medium`, because a sealed
   value redeems once.

If the second row stays `Passive`, the browser tests didn't run. The cloud
you're using may not have the tests yet, or the Resource Key may not
include `HumanConfidenceSource`.

If the page says the Resource Key doesn't carry Human Confidence, the
cloud lists no such property for the key. The page links to contact us,
where 51Degrees can add it to the account.

## Waiting for the browser tests

Naming HumanConfidence holds back the client script's complete handlers,
which this page uses, until the response to the request that carries the
browser test answers. That's the time the tests take and one more
request. A page can read the first response's Properties sooner through
the client script's change handlers.

## What a page's policy must allow

The browser tests run in the visitor's browser. A page that sets a
Content Security Policy has to allow these, or it runs no tests and stays
`Passive`. The cloud's host is the one in `_51DEGREES_CLOUD_ENDPOINT`, and
this page also loads its client script from, and sends its answers to,
this server.

| Directive | Must allow |
| --- | --- |
| `script-src` | The cloud's host, this server, and `'unsafe-eval'` |
| `connect-src` | The cloud's host, and this server |
| `worker-src` | `blob:` |

## Running more than one server

The Pipeline keeps the transaction it gives each page in the memory of
the server that gave it, for ten minutes, and keeps at most 10,000 pages,
the oldest giving way. `SetHumanConfidencePageLifetimeSeconds` and
`SetHumanConfidencePageCapacity` on the Cloud Request Engine's builder
change both. A page's later requests, and the one that sends the sealed
value back, have to reach the same server, so behind a load balancer use
sticky sessions. A sealed value sent to another server isn't redeemed,
and gives `Medium`.

Every view of the page takes one of those places. On a busy site, or one
open to anyone, limit how often a client can ask for the page, so that
requests made only to fill the store can't push real pages out.

## What a run costs

Every call to the cloud is one use against the subscription behind the
keys. A page load makes several, all from the server's Pipeline: the
passive call, one for the client script and each request it makes, and
the call that redeems. The Pipeline's check of the key at start-up is one
more, made once.

## Tests

`../../Tests/HumanConfidence.Examples.Tests` runs the example with no
Resource Key, reads these files for the rules every Human Confidence web
example keeps, reads the two Properties through the example's Pipeline
against a stand-in for the cloud, and, with a key in
`_51DEGREES_RESOURCE_KEY_BESPOKE`, asks the live cloud whether the key
carries `HumanConfidence`.

The tests of the passive level, and of a key outside the Bespoke product
getting a null source with a reason, need a cloud that serves
`HumanConfidenceSource`, which the production cloud does not yet. They call
the staging cloud in `CLOUD_STAGING_ENDPOINT` with the key in
`RESOURCE_KEY_BESPOKE_STAGING` and `_51DEGREES_RESOURCE_KEY_BASIC`. Only CI
holds the first two names, and
`.github/workflows/cloud-human-confidence.yml` runs these tests. Without a
name it needs, a live test skips and names it.
