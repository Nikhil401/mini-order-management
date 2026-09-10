import { Routes } from '@angular/router';
import { DashboardPage } from './pages/dashboard/dashboard.page';
import { OrdersPage } from './pages/orders/orders.page';
import { InventoryPage } from './pages/inventory/inventory.page';
import { HealthPage } from './pages/health/health.page';

export const routes: Routes = [
  { path: '', component: DashboardPage },
  { path: 'orders', component: OrdersPage },
  { path: 'inventory', component: InventoryPage },
  { path: 'health', component: HealthPage },
  { path: '**', redirectTo: '' }
];
