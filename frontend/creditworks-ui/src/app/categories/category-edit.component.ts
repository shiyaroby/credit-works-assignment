import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../core/api.service';
import { ErrorListComponent } from '../shared/error-list.component';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink, ErrorListComponent],
  templateUrl: './category-edit.component.html',
  styleUrls: ['./category-edit.component.scss'],
})
export class CategoryEditComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);

  id: number | null = null;
  icons: string[] = [];
  errors: string[] = [];
  submitting = false;
  loading = false;

  model: {
    name: string;
    minWeightKg: number;
    maxWeightKg: number | null;
    iconName: string;
  } = { name: '', minWeightKg: 0, maxWeightKg: null, iconName: 'light.svg' };

  get isEdit(): boolean {
    return this.id !== null;
  }

  ngOnInit(): void {
    const param = this.route.snapshot.paramMap.get('id');
    this.id = param ? Number(param) : null;

    if (!this.isEdit) {
      this.api
        .getIcons()
        .pipe(finalize(() => this.cdr.detectChanges()))
        .subscribe({
          next: (i) => {
            this.icons = i;
            if (i.length) this.model.iconName = i[0];
          },
          error: () => {
            this.errors = ['Failed to load icons.'];
          },
        });
      return;
    }

    this.loading = true;
    this.api.getIcons().subscribe({
      next: (icons) => {
        this.icons = icons;
        this.api.getCategories().subscribe({
          next: (list) => {
            const found = list.find((c) => c.id === this.id);
            if (found) this.model = { ...found };
            this.loading = false;
            this.cdr.detectChanges();
          },
          error: () => {
            this.errors = ['Failed to load category.'];
            this.loading = false;
            this.cdr.detectChanges();
          },
        });
      },
      error: () => {
        this.errors = ['Failed to load icons.'];
        this.loading = false;
        this.cdr.detectChanges();
      },
    });
  }

  submit(form: NgForm): void {
    this.errors = [];
    if (form.invalid) {
      this.errors.push('Please complete the form.');
      return;
    }

    const payload = {
      name: this.model.name,
      minWeightKg: this.model.minWeightKg,
      maxWeightKg:
        this.model.maxWeightKg === null || (this.model.maxWeightKg as any) === ''
          ? null
          : this.model.maxWeightKg,
      iconName: this.model.iconName,
    };

    this.submitting = true;
    const req = this.isEdit
      ? this.api.updateCategory(this.id!, payload as any)
      : this.api.createCategory({
          name: payload.name,
          minWeightKg: payload.minWeightKg,
          iconName: payload.iconName,
        });

    req.subscribe({
      next: () => this.router.navigate(['/categories']),
      error: (err) => {
        this.submitting = false;
        const body = err?.error;

        if (body?.suggestedAction === 'bulk-edit' && this.isEdit) {
          const proceed = confirm(
            "Changing this category's weight range on its own would leave a gap " +
              'or overlap until a neighbouring category is adjusted too.\n\n' +
              'Open the bulk editor to change both rows in a single save?',
          );
          if (proceed) {
            this.router.navigate(['/categories/bulk']);
            return;
          }
        }

        this.errors = body?.errors ?? ['Save failed.'];
        this.cdr.detectChanges();
      },
    });
  }
}
