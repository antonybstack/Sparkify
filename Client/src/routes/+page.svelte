<script lang="ts">
  import { writable } from "svelte/store";
  import { onMount } from "svelte";

  const account = writable({} as Account);
  const payments = writable([] as PaymentType[]);

  let eventSource: EventSource;

  onMount(() => {
    eventSource = new EventSource(`http://localhost:6002/api/payment/sse`);
    eventSource.addEventListener("account", function (event) {
      console.log("Received account event: ", event);
      account.set(JSON.parse(event.data));
    });
    eventSource.onmessage = function (event) {
      if (event.data === "heartbeat") {
        console.log("Received heartbeat from server");
      } else {
        console.log("Received message event: ", event);
        var test = JSON.parse(event.data);
        payments.update((data) => [JSON.parse(event.data), ...data]);
      }
    };

    eventSource.onerror = function (error) {
      console.error("EventSource failed:", error);
      if (eventSource.readyState === EventSource.CLOSED) {
        console.log("EventSource closed");
        // retry connection
        eventSource = new EventSource(`http://localhost:6002/api/payment/sse`);
      }
    };
  });

  async function postPayment() {
    try {
      const res = await fetch("http://localhost:6002/api/payment", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          id: null,
          // random amount
          amount: Math.floor(Math.random() * 1000),
          eventType: "PaymentRequested",
          referenceId: `${$account.Id}`,
        }),
      });

      if (res.ok) {
        console.log("Payment request succeeded: ", await res.json());
      } else {
        console.log("Payment request failed");
      }
    } catch (err) {
      console.log("Payment request errored out: " + err);
    }
  }

  class PaymentType {
    Id: string;
    Amount: number;
    EventType: number;
    ReferenceId: string;
    constructor(
      Id: string,
      Amount: number,
      EventType: number,
      ReferenceId: string
    ) {
      this.Id = Id;
      this.Amount = Amount;
      this.EventType = EventType;
      this.ReferenceId = ReferenceId;
    }
  }

  class Account {
    Id: string;
    FullName: string;
    Balance: number;
    constructor(Id: string, FullName: string, Balance: number) {
      this.Id = Id;
      this.FullName = FullName;
      this.Balance = Balance;
    }
  }
</script>

{#if $account.Id != null}
  <h2>Account: {$account.Id}</h2>
  <h3>Balance: ${$account.Balance}</h3>
  <h3>Full Name: {$account.FullName}</h3>
{/if}
<button on:click={postPayment}>Post Payment</button>
<p />

{#if $payments.length > 0}
  <h3>Payments:</h3>
  {#each $payments as data}
    <div>Id: {data.Id}</div>
    <div>Amount: ${data.Amount}</div>
    <div>EventType: {data.EventType}</div>
    <div>ReferenceId: {data.ReferenceId}</div>
    <div>&nbsp;</div>
  {/each}
{/if}
