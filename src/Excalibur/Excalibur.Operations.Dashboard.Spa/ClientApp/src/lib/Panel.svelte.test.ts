import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/svelte";
import { createRawSnippet } from "svelte";

import Panel from "./Panel.svelte";

// A trivial body snippet so the required `children` Snippet prop is satisfied.
function bodySnippet(text: string) {
  return createRawSnippet(() => ({
    render: () => `<p data-testid="body">${text}</p>`,
  }));
}

describe("Panel.svelte", () => {
  it("renders the title heading and the body content", () => {
    render(Panel, { props: { title: "Outbox", children: bodySnippet("hello") } });

    expect(screen.getByRole("heading", { name: "Outbox" })).toBeInTheDocument();
    expect(screen.getByTestId("body")).toHaveTextContent("hello");
  });

  it("renders the status line when a status is supplied", () => {
    render(Panel, {
      props: { title: "Inbox", status: "not configured", children: bodySnippet("x") },
    });

    expect(screen.getByText("not configured")).toBeInTheDocument();
  });

  it("omits the status line when no status is supplied", () => {
    const { container } = render(Panel, {
      props: { title: "Leader election", children: bodySnippet("x") },
    });

    expect(container.querySelector(".status")).toBeNull();
  });
});
