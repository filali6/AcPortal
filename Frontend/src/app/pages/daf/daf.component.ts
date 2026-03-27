import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-daf',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './daf.component.html',
  styleUrl: './daf.component.scss'
})
export class DafComponent {

  loading = false;
  successMessage = '';
  errorMessage = '';

  constructor(private http: HttpClient) {}

  signerContrat(): void {
    this.loading = true;
    this.successMessage = '';
    this.errorMessage = '';

    this.http.post(`${environment.apiUrl}/events/trigger-contract`, {}).subscribe({
      next: () => {
        this.successMessage = 'Contrat signé — le projet va être créé !';
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Erreur lors de la signature du contrat.';
        this.loading = false;
      }
    });
  }
}