export interface InventoryItem {
  productId: number;
  name: string;
  quantity: number;
}

export interface AddInventoryStockRequest {
  productId: number;
  name: string;
  quantity: number;
}

export interface InventoryResponse {
  productId: number;
  name: string;
  quantity: number;
}
