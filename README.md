# NDD.Umbraco.ContentReview

Review dates and reminders for Umbraco content.

Set a review date and an owner on a page or a section. When the date falls due,
the owner is reminded. It's built for sites that have to keep pages current,
such as universities, councils and other public bodies, and especially for
accessibility statements.

> **Status: alpha.** The package structure exists, but the review features
> haven't been built yet.

## Scope

This package records **review dates and owners, and sends reminders**. That's
all it does. It has no approval workflow, it never blocks saving or publishing,
and it has no sign-off chains. If you need those, see
[Umbraco Workflow](https://umbraco.com/products/add-ons/workflow).

## Install

Requires **Umbraco 17** (.NET 10).

```
dotnet add package NDD.Umbraco.ContentReview
```

If you use [Umbraco Automate](https://umbraco.com/products/add-ons/automate/)
and want review reminders available as an Automate trigger, also install:

```
dotnet add package NDD.Umbraco.ContentReview.Automate
```

The main package does not depend on Automate and works fully without it.

## Licence

MIT. Copyright © Norwood Design & Development Ltd.
