import {
  Component,
  OnInit,
  signal,
  computed,
  inject,
  ChangeDetectionStrategy,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';

// Angular Material
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialogModule, MatDialog, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatCheckboxModule } from '@angular/material/checkbox';

import { Product, ProductsService, CreateProductPayload, UpdateProductPayload } from './products.service';

// ─── Confirm Dialog ───────────────────────────────────────────────────────────

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [MatButtonModule, MatDialogModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6" style="max-width:360px">
      <div class="flex items-center gap-3 mb-4">
        <span class="material-icons text-red-500 text-3xl">warning</span>
        <h2 class="text-lg font-semibold text-gray-800">Confirmar exclusão</h2>
      </div>
      <p class="text-gray-600 mb-6">
        Tem certeza que deseja excluir este produto? Esta ação não pode ser desfeita.
      </p>
      <div class="flex justify-end gap-3">
        <button mat-stroked-button mat-dialog-close>Cancelar</button>
        <button mat-flat-button color="warn" [mat-dialog-close]="true">Excluir</button>
      </div>
    </div>
  `,
})
export class ConfirmDialogComponent {}

// ─── Product Form Dialog ──────────────────────────────────────────────────────

export interface ProductFormDialogData {
  product: Product | null;
  categories: string[];
}

@Component({
  selector: 'app-product-form-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div style="min-width:500px;max-width:560px">
      <h2 mat-dialog-title class="!text-xl !font-semibold">
        {{ data.product ? 'Editar Produto' : 'Novo Produto' }}
      </h2>

      <mat-dialog-content>
        <form [formGroup]="form" class="grid grid-cols-2 gap-x-4 gap-y-0 pt-2">

          <!-- Name -->
          <mat-form-field class="col-span-2" appearance="outline">
            <mat-label>Nome *</mat-label>
            <input matInput formControlName="name" placeholder="Ex.: Camiseta Branca" />
            @if (form.get('name')?.hasError('required') && form.get('name')?.touched) {
              <mat-error>Nome é obrigatório</mat-error>
            }
            @if (form.get('name')?.hasError('maxlength')) {
              <mat-error>Máximo 200 caracteres</mat-error>
            }
          </mat-form-field>

          <!-- Description -->
          <mat-form-field class="col-span-2" appearance="outline">
            <mat-label>Descrição</mat-label>
            <textarea matInput formControlName="description" rows="3" placeholder="Descreva o produto..."></textarea>
          </mat-form-field>

          <!-- Price -->
          <mat-form-field appearance="outline">
            <mat-label>Preço (R$) *</mat-label>
            <input matInput type="number" formControlName="price" min="0" step="0.01" placeholder="0,00" />
            @if (form.get('price')?.hasError('min') && form.get('price')?.touched) {
              <mat-error>Preço não pode ser negativo</mat-error>
            }
          </mat-form-field>

          <!-- Category -->
          <mat-form-field appearance="outline">
            <mat-label>Categoria</mat-label>
            <mat-select formControlName="category">
              <mat-option value="">Nenhuma</mat-option>
              @for (cat of data.categories; track cat) {
                <mat-option [value]="cat">{{ cat }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <!-- SKU (only on create) -->
          @if (!data.product) {
            <mat-form-field appearance="outline">
              <mat-label>SKU</mat-label>
              <input matInput formControlName="sku" placeholder="Código do produto" />
            </mat-form-field>
          }

          <!-- External Link -->
          <mat-form-field [class.col-span-2]="!!data.product" appearance="outline">
            <mat-label>Link externo</mat-label>
            <input matInput formControlName="externalLink" placeholder="https://..." />
          </mat-form-field>

          <!-- Image Upload -->
          <div class="col-span-2 mb-3">
            <p class="text-sm font-medium text-gray-700 mb-2">Imagem do produto</p>
            <div class="flex items-start gap-4">
              <div class="w-24 h-24 rounded-lg border-2 border-dashed border-gray-300 overflow-hidden flex items-center justify-center bg-gray-50 flex-shrink-0">
                @if (imagePreview()) {
                  <img [src]="imagePreview()" alt="Preview" class="w-full h-full object-cover" />
                } @else {
                  <span class="material-icons text-gray-400 text-4xl">image</span>
                }
              </div>
              <div class="flex-1">
                <button type="button" mat-stroked-button (click)="fileInput.click()" class="mb-2">
                  <span class="material-icons text-base mr-1">upload</span>
                  Escolher imagem
                </button>
                <input #fileInput type="file" accept="image/*" class="hidden" (change)="onFileChange($event)" />
                <p class="text-xs text-gray-500">PNG, JPG ou WEBP. Máx. 2 MB.</p>
                @if (imagePreview()) {
                  <button type="button" mat-button color="warn" class="!text-xs mt-1" (click)="clearImage()">
                    Remover imagem
                  </button>
                }
              </div>
            </div>
          </div>

          <!-- Stock section (only on create) -->
          @if (!data.product) {
            <div class="col-span-2">
              <mat-checkbox formControlName="trackStock">Controlar estoque</mat-checkbox>
            </div>
            @if (form.get('trackStock')?.value) {
              <mat-form-field appearance="outline" class="col-span-2 mt-2">
                <mat-label>Quantidade inicial</mat-label>
                <input matInput type="number" formControlName="stockQuantity" min="0" />
              </mat-form-field>
            }
          }

        </form>
      </mat-dialog-content>

      <mat-dialog-actions class="!px-6 !pb-5 flex justify-end gap-3">
        <button mat-stroked-button mat-dialog-close [disabled]="saving()">Cancelar</button>
        <button mat-flat-button color="primary" [disabled]="form.invalid || saving()" (click)="save()">
          @if (saving()) {
            <mat-progress-spinner diameter="18" mode="indeterminate" class="inline-block mr-2"></mat-progress-spinner>
          }
          {{ data.product ? 'Salvar alterações' : 'Criar produto' }}
        </button>
      </mat-dialog-actions>
    </div>
  `,
})
export class ProductFormDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  readonly dialogRef = inject(MatDialogRef<ProductFormDialogComponent>);
  readonly data = inject<ProductFormDialogData>(MAT_DIALOG_DATA);

  saving = signal(false);
  imagePreview = signal<string | null>(null);
  private _imageFile: string | null = null;

  form!: FormGroup;

  ngOnInit() {
    const p = this.data.product;
    this.form = this.fb.group({
      name: [p?.name ?? '', [Validators.required, Validators.maxLength(200)]],
      description: [p?.description ?? ''],
      price: [p?.price ?? 0, [Validators.required, Validators.min(0)]],
      category: [p?.category ?? ''],
      externalLink: [p?.externalLink ?? ''],
      sku: [''],
      trackStock: [false],
      stockQuantity: [0, Validators.min(0)],
    });

    if (p?.imageUrl) {
      this.imagePreview.set(p.imageUrl);
    }
  }

  onFileChange(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    if (file.size > 2 * 1024 * 1024) {
      alert('Imagem muito grande. Máximo 2 MB.');
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result as string;
      this._imageFile = result;
      this.imagePreview.set(result);
    };
    reader.readAsDataURL(file);
  }

  clearImage() {
    this._imageFile = null;
    this.imagePreview.set(null);
  }

  buildPayload(): CreateProductPayload | UpdateProductPayload {
    const v = this.form.value;
    const imageUrl = this._imageFile ?? (this.data.product?.imageUrl || undefined);
    const p = this.data.product;

    if (p) {
      return {
        id: p.id,
        name: v.name,
        description: v.description ?? '',
        price: Number(v.price),
        imageUrl,
        externalLink: v.externalLink || undefined,
        category: v.category || undefined,
      } as UpdateProductPayload;
    }

    return {
      name: v.name,
      description: v.description ?? '',
      price: Number(v.price),
      imageUrl,
      externalLink: v.externalLink || undefined,
      category: v.category || undefined,
      sku: v.sku || undefined,
      trackStock: v.trackStock ?? false,
      stockQuantity: v.trackStock ? (Number(v.stockQuantity) || 0) : 0,
    } as CreateProductPayload;
  }

  save() {
    if (this.form.invalid) return;
    this.saving.set(true);
    this.dialogRef.close(this.buildPayload());
  }
}

// ─── Main Products Component ──────────────────────────────────────────────────

@Component({
  selector: 'app-products',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatSnackBarModule,
    MatTooltipModule,
  ],
  template: `
    <div class="p-6">

      <!-- Header -->
      <div class="flex items-center justify-between mb-6">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">Produtos</h1>
          <p class="text-sm text-gray-500 mt-1">Gerencie o catálogo de produtos</p>
        </div>
        <button mat-flat-button color="primary" (click)="openCreateDialog()">
          <span class="material-icons text-base mr-1">add</span>
          Novo produto
        </button>
      </div>

      <!-- Filters -->
      <div class="flex flex-wrap gap-4 mb-6">
        <mat-form-field appearance="outline" class="flex-1 min-w-48">
          <mat-label>Buscar por nome</mat-label>
          <input matInput [(ngModel)]="searchQuery" (ngModelChange)="onFilterChange()" placeholder="Ex.: Camiseta..." />
          @if (searchQuery) {
            <button mat-icon-button matSuffix (click)="clearSearch()">
              <span class="material-icons text-base">close</span>
            </button>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" style="min-width:180px">
          <mat-label>Categoria</mat-label>
          <mat-select [(ngModel)]="selectedCategory" (ngModelChange)="onFilterChange()">
            <mat-option value="">Todas</mat-option>
            @for (cat of availableCategories(); track cat) {
              <mat-option [value]="cat">{{ cat }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" style="min-width:160px">
          <mat-label>Status</mat-label>
          <mat-select [(ngModel)]="statusFilter" (ngModelChange)="onFilterChange()">
            <mat-option value="all">Todos</mat-option>
            <mat-option value="active">Ativos</mat-option>
            <mat-option value="inactive">Inativos</mat-option>
          </mat-select>
        </mat-form-field>
      </div>

      <!-- Loading -->
      @if (loading()) {
        <div class="flex justify-center items-center h-48">
          <mat-progress-spinner diameter="40" mode="indeterminate"></mat-progress-spinner>
        </div>
      }

      <!-- Error -->
      @if (error() && !loading()) {
        <div class="bg-red-50 border border-red-200 rounded-lg p-4 mb-4 flex items-center gap-3">
          <span class="material-icons text-red-500">error_outline</span>
          <div>
            <p class="font-medium text-red-700">Erro ao carregar produtos</p>
            <p class="text-sm text-red-600">{{ error() }}</p>
          </div>
          <button mat-stroked-button color="warn" class="ml-auto" (click)="loadProducts()">Tentar novamente</button>
        </div>
      }

      <!-- Empty state -->
      @if (!loading() && !error() && filteredProducts().length === 0) {
        <div class="flex flex-col items-center justify-center h-48 text-gray-400">
          <span class="material-icons text-6xl mb-3">inventory_2</span>
          <p class="text-lg font-medium">Nenhum produto encontrado</p>
          <p class="text-sm mt-1">Ajuste os filtros ou crie um novo produto</p>
        </div>
      }

      <!-- Product Grid -->
      @if (!loading() && pagedProducts().length > 0) {
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4 mb-6">
          @for (product of pagedProducts(); track product.id) {
            <mat-card class="flex flex-col overflow-hidden hover:shadow-md transition-shadow">
              <!-- Image -->
              <div class="relative h-40 bg-gray-100 overflow-hidden">
                @if (product.imageUrl) {
                  <img
                    [src]="product.imageUrl"
                    [alt]="product.name"
                    class="w-full h-full object-cover"
                    (error)="onImgError($event)"
                  />
                } @else {
                  <div class="w-full h-full flex items-center justify-center">
                    <span class="material-icons text-5xl text-gray-300">image</span>
                  </div>
                }
                <!-- Status badge -->
                <span
                  class="absolute top-2 right-2 text-xs font-semibold px-2 py-0.5 rounded-full"
                  [class]="product.status === 'Active'
                    ? 'bg-green-100 text-green-700'
                    : 'bg-gray-200 text-gray-600'"
                >
                  {{ product.status === 'Active' ? 'Ativo' : 'Inativo' }}
                </span>
              </div>

              <mat-card-content class="flex flex-col flex-1 !pt-3 !pb-0 !px-3">
                @if (product.category) {
                  <span class="text-xs text-indigo-600 font-medium mb-1">{{ product.category }}</span>
                }
                <h3 class="font-semibold text-gray-900 text-sm leading-tight mb-1 line-clamp-2">{{ product.name }}</h3>
                <p class="text-lg font-bold text-green-600 mt-auto pt-2">{{ product.priceFormatted }}</p>
                @if (product.trackStock) {
                  <p class="text-xs text-gray-500 mt-0.5 pb-1">Estoque: {{ product.stockQuantity }} un.</p>
                }
              </mat-card-content>

              <mat-card-actions class="!px-3 !pb-3 !pt-2 flex items-center justify-between">
                <mat-slide-toggle
                  [checked]="product.status === 'Active'"
                  (change)="toggleStatus(product)"
                  [matTooltip]="product.status === 'Active' ? 'Desativar produto' : 'Ativar produto'"
                  color="primary"
                  class="scale-90"
                  [disabled]="togglingId() === product.id"
                ></mat-slide-toggle>

                <div class="flex gap-1">
                  <button mat-icon-button matTooltip="Editar" (click)="openEditDialog(product)" class="!w-8 !h-8">
                    <span class="material-icons text-base text-gray-600">edit</span>
                  </button>
                  <button mat-icon-button matTooltip="Excluir" (click)="confirmDelete(product)" class="!w-8 !h-8">
                    <span class="material-icons text-base text-red-400">delete</span>
                  </button>
                </div>
              </mat-card-actions>
            </mat-card>
          }
        </div>

        <!-- Pagination -->
        <mat-paginator
          [length]="filteredProducts().length"
          [pageSize]="pageSize"
          [pageSizeOptions]="[8, 16, 32]"
          [pageIndex]="pageIndex"
          (page)="onPageChange($event)"
          showFirstLastButtons
        ></mat-paginator>
      }
    </div>
  `,
})
export class ProductsComponent implements OnInit {
  private readonly svc = inject(ProductsService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  loading = signal(true);
  error = signal<string | null>(null);
  allProducts = signal<Product[]>([]);
  togglingId = signal<string | null>(null);

  searchQuery = '';
  selectedCategory = '';
  statusFilter: 'all' | 'active' | 'inactive' = 'all';

  pageSize = 8;
  pageIndex = 0;

  availableCategories = computed(() => {
    const cats = this.allProducts()
      .map((p) => p.category)
      .filter((c): c is string => !!c);
    return [...new Set(cats)].sort();
  });

  filteredProducts = computed(() => {
    let products = this.allProducts();

    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      products = products.filter((p) => p.name.toLowerCase().includes(q));
    }

    if (this.selectedCategory) {
      products = products.filter((p) => p.category === this.selectedCategory);
    }

    if (this.statusFilter === 'active') {
      products = products.filter((p) => p.status === 'Active');
    } else if (this.statusFilter === 'inactive') {
      products = products.filter((p) => p.status !== 'Active');
    }

    return products;
  });

  pagedProducts = computed(() => {
    const start = this.pageIndex * this.pageSize;
    return this.filteredProducts().slice(start, start + this.pageSize);
  });

  ngOnInit() {
    this.loadProducts();
  }

  loadProducts() {
    this.loading.set(true);
    this.error.set(null);
    this.svc.getAll({ activeOnly: false }).subscribe({
      next: (products) => {
        this.allProducts.set(products);
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(err.error?.error ?? 'Erro ao conectar com a API');
        this.loading.set(false);
      },
    });
  }

  onFilterChange() {
    this.pageIndex = 0;
  }

  clearSearch() {
    this.searchQuery = '';
    this.pageIndex = 0;
  }

  onPageChange(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
  }

  onImgError(event: Event) {
    (event.target as HTMLImageElement).style.display = 'none';
  }

  openCreateDialog() {
    const ref = this.dialog.open(ProductFormDialogComponent, {
      width: '560px',
      maxWidth: '95vw',
      data: { product: null, categories: this.availableCategories() } satisfies ProductFormDialogData,
    });

    ref.afterClosed().subscribe((payload: CreateProductPayload | undefined) => {
      if (!payload) return;
      this.svc.create(payload).subscribe({
        next: (created) => {
          this.allProducts.update((list) => [created, ...list]);
          this.snackBar.open('Produto criado com sucesso!', 'Fechar', { duration: 3000 });
        },
        error: (err: HttpErrorResponse) => {
          this.snackBar.open(err.error?.error ?? 'Erro ao criar produto', 'Fechar', { duration: 4000 });
        },
      });
    });
  }

  openEditDialog(product: Product) {
    const ref = this.dialog.open(ProductFormDialogComponent, {
      width: '560px',
      maxWidth: '95vw',
      data: { product, categories: this.availableCategories() } satisfies ProductFormDialogData,
    });

    ref.afterClosed().subscribe((payload: UpdateProductPayload | undefined) => {
      if (!payload) return;
      this.svc.update(payload).subscribe({
        next: (updated) => {
          this.allProducts.update((list) =>
            list.map((p) => (p.id === updated.id ? updated : p))
          );
          this.snackBar.open('Produto atualizado!', 'Fechar', { duration: 3000 });
        },
        error: (err: HttpErrorResponse) => {
          this.snackBar.open(err.error?.error ?? 'Erro ao atualizar produto', 'Fechar', { duration: 4000 });
        },
      });
    });
  }

  toggleStatus(product: Product) {
    this.togglingId.set(product.id);
    if (product.status === 'Active') {
      this.svc.delete(product.id).subscribe({
        next: () => {
          this.allProducts.update((list) =>
            list.map((p) => (p.id === product.id ? { ...p, status: 'Inactive', isAvailable: false } : p))
          );
          this.togglingId.set(null);
          this.snackBar.open('Produto desativado.', 'Fechar', { duration: 2500 });
        },
        error: (err: HttpErrorResponse) => {
          this.togglingId.set(null);
          this.snackBar.open(err.error?.error ?? 'Erro ao desativar produto', 'Fechar', { duration: 3000 });
        },
      });
    } else {
      const payload: UpdateProductPayload = {
        id: product.id,
        name: product.name,
        description: product.description,
        price: product.price,
        imageUrl: product.imageUrl,
        externalLink: product.externalLink,
        category: product.category,
      };
      this.svc.update(payload).subscribe({
        next: (updated) => {
          this.allProducts.update((list) =>
            list.map((p) => (p.id === updated.id ? updated : p))
          );
          this.togglingId.set(null);
          this.snackBar.open('Produto ativado.', 'Fechar', { duration: 2500 });
        },
        error: (err: HttpErrorResponse) => {
          this.togglingId.set(null);
          this.snackBar.open(err.error?.error ?? 'Erro ao ativar produto', 'Fechar', { duration: 3000 });
        },
      });
    }
  }

  confirmDelete(product: Product) {
    const ref = this.dialog.open(ConfirmDialogComponent, { width: '380px' });
    ref.afterClosed().subscribe((confirmed) => {
      if (!confirmed) return;
      this.svc.delete(product.id).subscribe({
        next: () => {
          this.allProducts.update((list) => list.filter((p) => p.id !== product.id));
          this.snackBar.open('Produto excluído.', 'Fechar', { duration: 3000 });
        },
        error: (err: HttpErrorResponse) => {
          this.snackBar.open(err.error?.error ?? 'Erro ao excluir produto', 'Fechar', { duration: 3000 });
        },
      });
    });
  }
}
