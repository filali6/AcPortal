import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProjectsService } from '../../../core/services/projects.service';
import { ToastService } from '../../../core/services/toast.service';
import { LucideAngularModule, ChevronRight, Edit2, X ,Trash2,Plus} from 'lucide-angular';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { TabsService } from '../../../core/services/tabs.service';
import { UtilsService } from '../../../core/services/utils.service';
import { ModalComponent } from '../../../core/components/modal/modal.component';
import { TranslateModule } from '@ngx-translate/core';
@Component({
  selector: 'app-portfolios-tab',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule,ModalComponent,TranslateModule],
  templateUrl: './portfolios-tab.component.html',
  styleUrl: './portfolios-tab.component.scss'
})
export class PortfoliosTabComponent implements OnInit {
  @Input() directors: any[] = [];

  portfolios: any[] = [];
  selectedPortfolio: any = null;
  loading = false;

  // Modal
  showModal = false;
  modalMinimized = false;
  modalPortfolio: any = null;
  selectedDirectorId = '';

  showEditModal = false;
editPortfolio: any = null;
editName = '';
editDescription = '';
showDeleteConfirm = false;
portfolioToDelete: any = null;


showCreateModal = false;
newName = '';
newDescription = '';
newDirectorId = '';


  

  readonly ChevronRight = ChevronRight;
  readonly Edit2 = Edit2;
  readonly X = X;
  readonly Trash2=Trash2;
  readonly Plus = Plus;

  constructor(
    private projectsService: ProjectsService,
    private toastService: ToastService,
    private http: HttpClient,
    private tabsService: TabsService,
    public utils:UtilsService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.projectsService.getAllPortfolios().subscribe({
      next: (p) => this.portfolios = p
    });
  }

  selectPortfolio(portfolio: any): void {
  
  this.tabsService.openTab({
    id: 'projects-tab',
    title: 'Projects',
    type: 'create-project',
    data: { portfolioFilter: portfolio.id, portfolioName: portfolio.name }
  });
}

  openModal(portfolio: any): void {
    this.modalPortfolio = portfolio;
    this.selectedDirectorId = portfolio.director?.id || '';
    this.showModal = true;
      
  }

  closeModal(): void {
    this.showModal = false;
    this.modalPortfolio = null;
    this.selectedDirectorId = '';
  }

  changeDirector(): void {
    if (!this.selectedDirectorId || !this.modalPortfolio) return;
    this.loading = true;
    this.projectsService.assignDirectorToPortfolio(this.modalPortfolio.id, this.selectedDirectorId).subscribe({
      next: () => {
        this.toastService.show('Director updated!', 'success');
        this.closeModal();
        this.loading = false;
        this.load();
      },
      error: () => {
        this.toastService.show('Error updating director', 'error');
        this.loading = false;
      }
    });
  }
  openEditModal(portfolio: any): void {
  this.editPortfolio = portfolio;
  this.editName = portfolio.name;
  this.editDescription = portfolio.description || '';
  this.showEditModal = true;
}

closeEditModal(): void {
  this.showEditModal = false;
  this.editPortfolio = null;
  this.editName = '';
  this.editDescription = '';
}

saveEdit(): void {
  if (!this.editName || !this.editPortfolio) return;
  this.loading = true;
  this.projectsService.updatePortfolio(
    this.editPortfolio.id,
    this.editName,
    this.editDescription
  ).subscribe({
    next: () => {
      this.toastService.show('Portfolio updated!', 'success');
      this.closeEditModal();
      this.loading = false;
      this.load();
    },
    error: () => {
      this.toastService.show('Error updating portfolio', 'error');
      this.loading = false;
    }
  });
}

openDeleteConfirm(portfolio: any): void {
  this.portfolioToDelete = portfolio;
  this.showDeleteConfirm = true;
}

closeDeleteConfirm(): void {
  this.showDeleteConfirm = false;
  this.portfolioToDelete = null;
}

confirmDelete(): void {
  if (!this.portfolioToDelete) return;
  this.loading = true;
  this.projectsService.deletePortfolio(this.portfolioToDelete.id).subscribe({
    next: () => {
      this.toastService.show('Portfolio deleted!', 'success');
      this.closeDeleteConfirm();
      this.loading = false;
      this.load();
    },
    error: () => {
      this.toastService.show('Error deleting portfolio', 'error');
      this.loading = false;
    }
  });
}
openCreateModal(): void {
  this.showCreateModal = true;
}

closeCreateModal(): void {
  this.showCreateModal = false;
  this.newName = '';
  this.newDescription = '';
  this.newDirectorId = '';
}

createPortfolio(): void {
  if (!this.newName || !this.newDirectorId) return;
  this.loading = true;
  this.projectsService.createPortfolio(
    this.newName, this.newDescription, this.newDirectorId
  ).subscribe({
    next: () => {
      this.toastService.show('Portfolio created!', 'success');
      this.closeCreateModal();
      this.loading = false;
      this.load();
    },
    error: () => {
      this.toastService.show('Error creating portfolio', 'error');
      this.loading = false;
    }
  });
}
}