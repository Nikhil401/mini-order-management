import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ReactiveFormsModule, Validators, FormBuilder } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { InventoryItem } from '../../core/models/inventory.models';

@Component({
  selector: 'app-inventory-page',
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <main class="page">
      <div class="heading">
        <div>
          <p class="eyebrow">Stock management</p>
          <h1>Inventory</h1>
          <p class="subtitle">Create a product or add quantity to an existing product ID.</p>
        </div>
      </div>

      <form class="stock-form" [formGroup]="stockForm" (ngSubmit)="addStock()">
        <label>
          <span>Product ID</span>
          <input type="number" min="1" formControlName="productId" placeholder="e.g. 104" />
        </label>
        <label>
          <span>Product name</span>
          <input type="text" maxlength="100" formControlName="name" placeholder="e.g. Monitor" />
        </label>
        <label>
          <span>Quantity to add</span>
          <input type="number" min="1" formControlName="quantity" />
        </label>
        <button type="submit" [disabled]="stockForm.invalid || saving()">
          {{ saving() ? 'Saving...' : 'Add stock' }}
        </button>
      </form>

      @if (message()) {
        <div class="message" [class.error]="hasError()">{{ message() }}</div>
      }

      <div class="panel">
        <div class="table-header"><span>Product ID</span><span>Name</span><span>In stock</span></div>
        @for (item of inventory(); track item.productId) {
          <div class="row">
            <strong>#{{ item.productId }}</strong>
            <span>{{ item.name }}</span>
            <strong class="quantity">{{ item.quantity }}</strong>
          </div>
        } @empty {
          <div class="empty">No inventory loaded yet.</div>
        }
      </div>
    </main>
  `,
  styles: [`
    .page{display:grid;gap:1.35rem}.heading h1{margin:.2rem 0;font-size:clamp(1.85rem,6vw,2.55rem);letter-spacing:-.055em}.eyebrow{margin:0;color:var(--primary);font-size:.7rem;font-weight:800;letter-spacing:.15em;text-transform:uppercase}.subtitle{margin:.35rem 0 0;color:var(--muted);line-height:1.55}.stock-form,.panel{border-radius:1.4rem;background:var(--surface);box-shadow:var(--raised)}.stock-form{display:grid;gap:1rem;padding:1.25rem}label{display:grid;gap:.45rem;color:var(--muted);font-size:.76rem;font-weight:800;text-transform:uppercase;letter-spacing:.07em}input{width:100%;padding:.8rem .9rem;border:0;border-radius:.82rem;color:var(--ink);background:var(--surface);box-shadow:var(--pressed);outline:none}input:focus{box-shadow:inset 4px 4px 9px var(--shadow-dark),inset -4px -4px 9px var(--shadow-light),0 0 0 3px rgba(92,110,232,.17)}button{padding:.8rem 1.1rem;border:0;border-radius:.82rem;color:#fff;background:linear-gradient(145deg,#7483ef,#4b5bd5);box-shadow:4px 4px 8px #c3cad4,-4px -4px 8px #fff;font-weight:800;cursor:pointer}button:disabled{opacity:.55;cursor:not-allowed}.message{padding:.9rem 1rem;border-radius:.9rem;color:#187457;background:#dff5eb;box-shadow:var(--pressed)}.message.error{color:#b84d5b;background:#f8e4e7}.panel{padding:1rem}.table-header,.row{display:grid;grid-template-columns:1fr 1.6fr .55fr;gap:.7rem;padding:.9rem .4rem}.table-header{color:var(--muted);text-transform:uppercase;font-size:.68rem;letter-spacing:.09em;font-weight:800}.row{border-top:1px solid #d8dee7}.row strong:first-child{color:var(--primary-dark)}.quantity{color:var(--success)}.empty{color:var(--muted);padding:.8rem}@media(min-width:680px){.stock-form{grid-template-columns:1fr 2fr 1fr auto;align-items:end}} 
  `]
})
export class InventoryPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly inventory = signal<InventoryItem[]>([]);
  protected readonly saving = signal(false);
  protected readonly message = signal('');
  protected readonly hasError = signal(false);
  protected readonly stockForm = this.formBuilder.nonNullable.group({
    productId: [104, [Validators.required, Validators.min(1)]],
    name: ['', [Validators.required, Validators.maxLength(100)]],
    quantity: [1, [Validators.required, Validators.min(1)]]
  });

  ngOnInit(): void {
    this.loadInventory();
  }

  protected addStock(): void {
    if (this.stockForm.invalid || this.saving()) return;

    this.saving.set(true);
    this.message.set('');
    this.api.addInventoryStock(this.stockForm.getRawValue()).subscribe({
      next: updatedItem => {
        this.inventory.update(items =>
          [...items.filter(item => item.productId !== updatedItem.productId), updatedItem]
            .sort((left, right) => left.productId - right.productId)
        );
        this.hasError.set(false);
        this.message.set(`Stock saved. ${updatedItem.name} now has ${updatedItem.quantity} item(s).`);
        this.stockForm.reset({ productId: updatedItem.productId, name: updatedItem.name, quantity: 1 });
        this.saving.set(false);
      },
      error: error => {
        this.hasError.set(true);
        this.message.set(error?.error?.message ?? 'Could not add stock. Please try again.');
        this.saving.set(false);
      }
    });
  }

  private loadInventory(): void {
    this.api.getInventory().subscribe({
      next: items => this.inventory.set(items),
      error: () => {
        this.hasError.set(true);
        this.message.set('Could not load inventory.');
      }
    });
  }
}
